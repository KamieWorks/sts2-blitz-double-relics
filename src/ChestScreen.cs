using System.Reflection;
using System.Reflection.Emit;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Runs;
using Synchronizer = MegaCrit.Sts2.Core.Multiplayer.Game.TreasureRoomRelicSynchronizer;

namespace BlitzDoubleRelics;

internal static class ChestScreen
{
    private static readonly AccessTools.FieldRef<Synchronizer, Action?> VotesChanged =
        AccessTools.FieldRefAccess<Synchronizer, Action?>(nameof(Synchronizer.VotesChanged));

    [HarmonyPatch(typeof(NTreasureRoomRelicCollection), nameof(NTreasureRoomRelicCollection.InitializeRelics))]
    private static class ExpandTray
    {
        private static void Prefix(List<Control> ____holderLayouts, Control ____relicContainer)
        {
            Synchronizer synchronizer = RunManager.Instance.TreasureRoomRelicSynchronizer;
            if (!ChestRounds.TryGet(synchronizer, out _) || synchronizer.CurrentRelics is not { } relics)
                return;

            while (____holderLayouts.Count < relics.Count)
            {
                int slots = ____holderLayouts.Count + 1;
                var layout = new Control { Name = $"BlitzRelicLayout{slots}", Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
                ____relicContainer.AddChild(layout);
                layout.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                var scene = GD.Load<PackedScene>("res://scenes/ui/treasure_relic_holder.tscn");
                for (int slot = 0; slot < slots; slot++)
                {
                    var holder = scene.Instantiate<NTreasureRoomRelicHolder>();
                    layout.AddChild(holder);
                    holder.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
                    int column = slot % 4;
                    int row = slot / 4;
                    int columnCount = Math.Min(4, slots - 4 * row);
                    float x = (column - (columnCount - 1) / 2f) * 200f;
                    float y = row == 0 ? -115f : 115f;
                    var halfSize = holder.CustomMinimumSize / 2f;
                    holder.OffsetLeft = x - halfSize.X;
                    holder.OffsetRight = x + halfSize.X;
                    holder.OffsetTop = y - halfSize.Y;
                    holder.OffsetBottom = y + halfSize.Y;
                }
                ____holderLayouts.Add(layout);
            }
        }
    }

    [HarmonyPatch(typeof(NTreasureRoomRelicCollection), "AnimateRelicAwards")]
    private static class AwaitFirstAnimation
    {
        private static void Prefix(bool isFinished, out ChestRounds.ChestRound? __state)
        {
            __state = null;
            if (!isFinished && ChestRounds.TryGet(RunManager.Instance.TreasureRoomRelicSynchronizer, out var round))
            {
                round.AnimatingFirstRound = true;
                __state = round;
            }
        }

        private static void Postfix(NTreasureRoomRelicCollection __instance, IRunState ____runState,
            NHandImageCollection ____hands, ChestRounds.ChestRound? __state, ref Task __result)
        {
            if (__state is not null)
            {
                __result = Finish(__result, __instance, ____runState, ____hands, __state,
                    RunManager.Instance.TreasureRoomRelicSynchronizer);
            }
        }

        private static void RestorePointingTexture(NHandImage hand)
        {
            hand.GetNode<TextureRect>("TextureRect").Texture = hand.Player.Character.ArmPointingTexture;
        }

        private static async Task Finish(Task animation, NTreasureRoomRelicCollection screen, IRunState runState,
            NHandImageCollection hands, ChestRounds.ChestRound round, Synchronizer synchronizer)
        {
            await animation;
            if (!GodotObject.IsInstanceValid(screen) || !screen.IsInsideTree())
                return;

            foreach (var player in runState.Players)
            {
                NHandImage? hand = hands.GetHand(player.NetId);
                if (hand is null)
                    continue;

                hand.SetFrozenForRelicAwards(false);
                RestorePointingTexture(hand);
            }

            round.AnimatingFirstRound = false;
            VotesChanged(synchronizer)?.Invoke();
        }
    }

    // A faster peer can vote before the local client has finished moving and reindexing the old holders.
    // Their vote is already synchronized. Defer the visual refresh until the local animation ends.
    [HarmonyPatch]
    private static class DeferVoteRefresh
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(NTreasureRoomRelicCollection), "RefreshVotes");
            yield return AccessTools.DeclaredMethod(typeof(NTreasureRoom), "RefreshVotes");
        }

        private static bool Prefix() =>
            !ChestRounds.TryGet(RunManager.Instance.TreasureRoomRelicSynchronizer, out var round) || !round.AnimatingFirstRound;
    }

    [HarmonyPatch]
    private static class RemoveAwardedHolders
    {
        private static MethodBase TargetMethod() => AccessTools.AsyncMoveNext(
            AccessTools.DeclaredMethod(typeof(NTreasureRoomRelicCollection), "AnimateRelicAwards"));

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo original = AccessTools.DeclaredMethod(typeof(List<NTreasureRoomRelicHolder>), nameof(List<NTreasureRoomRelicHolder>.RemoveAt));
            MethodInfo replacement = AccessTools.DeclaredMethod(typeof(ChestScreen), nameof(RemoveAnimatedHolder));
            var code = instructions.ToList();
            CodeInstruction call = code.Single(instruction => instruction.Calls(original));
            call.opcode = OpCodes.Call;
            call.operand = replacement;
            return code;
        }
    }

    private static void RemoveAnimatedHolder(List<NTreasureRoomRelicHolder> holders, int originalSlot)
    {
        if (!ChestRounds.TryGet(RunManager.Instance.TreasureRoomRelicSynchronizer, out var round) || !round.AnimatingFirstRound)
        {
            holders.RemoveAt(originalSlot);
            return;
        }
        RemoveByOriginalSlot(holders, originalSlot).QueueFree();
    }

    internal static NTreasureRoomRelicHolder RemoveByOriginalSlot(List<NTreasureRoomRelicHolder> holders, int originalSlot)
    {
        // The game reindexes after the full batch, so Index still identifies the original slot here.
        int currentSlot = holders.FindIndex(holder => holder.Index == originalSlot);
        var holder = holders[currentSlot];
        holders.RemoveAt(currentSlot);
        return holder;
    }
}
