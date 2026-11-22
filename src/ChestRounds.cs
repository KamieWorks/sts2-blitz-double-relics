using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.TreasureRelicPicking;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using Synchronizer = MegaCrit.Sts2.Core.Multiplayer.Game.TreasureRoomRelicSynchronizer;

namespace BlitzDoubleRelics;

internal static class ChestRounds
{
    private static readonly ConditionalWeakTable<Synchronizer, ChestRound> Sessions = new();

    internal static bool TryGet(Synchronizer? synchronizer, out ChestRound round)
    {
        round = null!;
        return synchronizer is not null && Sessions.TryGetValue(synchronizer, out round!);
    }

    internal static bool Applies(GameMode mode, int players, NetGameType netType, int originalRounds) =>
        originalRounds == 1 && mode == GameMode.Blitz && players is >= 2 and <= 4 &&
        netType is NetGameType.Host or NetGameType.Client;

    internal static ChestRound Start(Synchronizer synchronizer)
    {
        Sessions.Remove(synchronizer);
        return Sessions.GetOrCreateValue(synchronizer);
    }

    internal sealed class ChestRound
    {
        internal bool SecondRound { get; private set; }
        internal bool AnimatingFirstRound { get; set; }

        internal void Advance(IReadOnlyList<Synchronizer.PlayerVote> votes)
        {
            foreach (Synchronizer.PlayerVote vote in votes)
            {
                vote.index = null;
                vote.voteReceived = false;
            }
            SecondRound = true;
        }
    }

    [HarmonyPatch(typeof(Synchronizer), nameof(Synchronizer.BeginRelicPicking))]
    private static class BeginPicking
    {
        private static void Postfix(Synchronizer __instance, IRunState ____runState,
            List<RelicModel>? ____currentRelics, RelicGrabBag ____sharedGrabBag, Rng ____rng, ref int ____roundsLeft)
        {
            NetGameType netType = RunManager.Instance.NetService?.Type ?? NetGameType.None;
            if (____currentRelics is null || !Applies(____runState.GameMode, ____runState.Players.Count, netType, ____roundsLeft))
                return;

            // Use the game's shared grab bag and seeded RNG on every participant.
            int needed = 2 * ____runState.Players.Count;
            while (____currentRelics.Count < needed)
            {
                var rarity = RelicFactory.RollRarity(____rng);
                ____currentRelics.Add(____sharedGrabBag.PullFromFront(rarity, ____runState) ?? RelicFactory.FallbackRelic);
            }
            ____roundsLeft = 2;
            Start(__instance);
            Log.Info($"{ModEntry.LogPrefix} Opened {____runState.Players.Count}-player chest with {____currentRelics.Count} candidates and 2 rounds.");
        }
    }

    [HarmonyPatch(typeof(Synchronizer), nameof(Synchronizer.PickRelicLocally))]
    private static class PickLocally
    {
        private static bool Prefix(Synchronizer __instance, IRunState ____runState, ulong ____localPlayerId)
        {
            if (!TryGet(__instance, out ChestRound round))
                return true;

            // Each peer queues at most one native pick action per round. An old click cannot spill into round two.
            Player? localPlayer = ____runState.GetPlayer(____localPlayerId);
            return !round.AnimatingFirstRound && __instance.CurrentRelics is not null && localPlayer is not null &&
                !__instance.GetPlayerVote(localPlayer).voteReceived;
        }
    }

    [HarmonyPatch(typeof(Synchronizer), nameof(Synchronizer.OnPicked))]
    private static class ReceivePick
    {
        private static bool Prefix(Synchronizer __instance, Player player, IRunState ____runState,
            List<Synchronizer.PlayerVote> ____votes)
        {
            return !TryGet(__instance, out _) || __instance.CurrentRelics is null ||
                !____votes[____runState.GetPlayerSlotIndex(player)].voteReceived;
        }

        private static void Postfix(Synchronizer __instance, int ____roundsLeft, List<Synchronizer.PlayerVote> ____votes)
        {
            if (!TryGet(__instance, out ChestRound round))
                return;
            if (____roundsLeft == 1 && !round.SecondRound)
            {
                round.Advance(____votes);
                Log.Info($"{ModEntry.LogPrefix} Round 1 resolved; {__instance.CurrentRelics?.Count ?? 0} candidates remain.");
            }
            if (____roundsLeft == 0)
            {
                Sessions.Remove(__instance);
                Log.Info($"{ModEntry.LogPrefix} Round 2 resolved.");
            }
        }
    }

    [HarmonyPatch(typeof(Synchronizer), "AwardRelics")]
    private static class CorrectRemainingOrder
    {
        private static void Prefix(Synchronizer __instance, List<RelicModel> ____currentRelics, out AwardSnapshot? __state)
        {
            __state = TryGet(__instance, out ChestRound round) && !round.SecondRound
                ? new AwardSnapshot(__instance, ____currentRelics)
                : null;
        }

        private static void Finalizer(AwardSnapshot? __state, Exception? __exception) =>
            __state?.Finish(apply: __exception is null);
    }

    // The stock code removes by model identity. Multiple fallback relics can share an identity, so the first
    // round must keep the exact surviving slots used by the UI and by the next synchronized action.
    internal sealed class AwardSnapshot
    {
        private readonly Synchronizer synchronizer;
        private readonly List<RelicModel> remaining;
        private readonly RelicModel[] startingOrder;
        private HashSet<int>? removed;

        internal AwardSnapshot(Synchronizer synchronizer, List<RelicModel> remaining)
        {
            this.synchronizer = synchronizer;
            this.remaining = remaining;
            startingOrder = remaining.ToArray();
            synchronizer.RelicsAwarded += Capture;
        }

        private void Capture(List<RelicPickingResult> results, bool _) =>
            removed = results.Select(result => result.relicIndex).ToHashSet();

        internal void Finish(bool apply)
        {
            synchronizer.RelicsAwarded -= Capture;
            if (!apply || removed is null)
                return;

            remaining.Clear();
            for (int slot = 0; slot < startingOrder.Length; slot++)
            {
                if (!removed.Contains(slot))
                    remaining.Add(startingOrder[slot]);
            }
        }
    }
}
