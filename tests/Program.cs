using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using BlitzDoubleRelics;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.TreasureRelicPicking;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Runs;
using Synchronizer = MegaCrit.Sts2.Core.Multiplayer.Game.TreasureRoomRelicSynchronizer;

if (args.Length != 1)
    throw new ArgumentException("Pass the game assembly directory after --.");

string gameDir = Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    string path = Path.Combine(gameDir, name.Name + ".dll");
    return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
};

Checks.Run();

internal static class Checks
{
    public static void Run()
    {
        CheckLiveAssemblyPatchTargets();
        CheckModeScope();
        CheckBuildScope();
        CheckVotesResetAndSessionReplacement();
        CheckSimultaneousSlotRemoval();
        CheckFallbackRelicOrder();
        Console.WriteLine("Six checks passed against the installed game and Harmony assemblies.");
    }

    private static void CheckLiveAssemblyPatchTargets()
    {
        ModEntry.ApplyPatches();
        var animation = AccessTools.DeclaredMethod(typeof(NTreasureRoomRelicCollection), "AnimateRelicAwards");
        MethodBase[] targets =
        [
            AccessTools.DeclaredMethod(typeof(Synchronizer), nameof(Synchronizer.BeginRelicPicking)),
            AccessTools.DeclaredMethod(typeof(Synchronizer), nameof(Synchronizer.PickRelicLocally)),
            AccessTools.DeclaredMethod(typeof(Synchronizer), nameof(Synchronizer.OnPicked)),
            AccessTools.DeclaredMethod(typeof(Synchronizer), "AwardRelics"),
            AccessTools.DeclaredMethod(typeof(NTreasureRoomRelicCollection), nameof(NTreasureRoomRelicCollection.InitializeRelics)),
            AccessTools.DeclaredMethod(typeof(NTreasureRoomRelicCollection), "RefreshVotes"),
            AccessTools.DeclaredMethod(typeof(NTreasureRoom), "RefreshVotes"),
            animation,
            AccessTools.AsyncMoveNext(animation)
        ];
        foreach (MethodBase target in targets)
            Require(Harmony.GetPatchInfo(target).Owners.Contains(ModEntry.HarmonyId), $"Patch missing on {target}");
    }

    private static void CheckModeScope()
    {
        foreach (int players in new[] { 2, 3, 4 })
        {
            Require(ChestRounds.Applies(GameMode.Blitz, players, NetGameType.Host, 1), "Host must run the rule");
            Require(ChestRounds.Applies(GameMode.Blitz, players, NetGameType.Client, 1), "Client must run the rule");
        }
        Require(!ChestRounds.Applies(GameMode.Blitz, 1, NetGameType.Singleplayer, 1), "Single-player Blitz is unchanged");
        Require(!ChestRounds.Applies(GameMode.Standard, 2, NetGameType.Host, 1), "Standard treasure is unchanged");
        Require(!ChestRounds.Applies(GameMode.Blitz, 2, NetGameType.Singleplayer, 1), "Fake multiplayer is excluded");
        Require(!ChestRounds.Applies(GameMode.Blitz, 2, NetGameType.Replay, 1), "Replay is excluded");
        Require(!ChestRounds.Applies(GameMode.Blitz, 2, NetGameType.Host, 2), "Keep the game's native two rounds");
    }

    private static void CheckBuildScope()
    {
        Require(ModEntry.SupportsBuild("v0.113.1", "d09d48e5"), "The supported build must load");
        Require(!ModEntry.SupportsBuild("v0.113.2", "d09d48e5"), "A newer game version must stay inactive");
        Require(!ModEntry.SupportsBuild("v0.113.1", "ffffffff"), "An unverified build of the same version must stay inactive");
        Require(!ModEntry.SupportsBuild("v0.113.1", null), "Missing build information must stay inactive");
    }

    private static void CheckVotesResetAndSessionReplacement()
    {
        var synchronizer = Uninitialized<Synchronizer>();
        var round = ChestRounds.Start(synchronizer);
        Synchronizer.PlayerVote[] votes = [new() { index = 3, voteReceived = true }, new() { voteReceived = true }];
        round.Advance(votes);
        Require(round.SecondRound && votes.All(vote => vote.index is null && !vote.voteReceived),
            "Both a pick and a skip must be cleared before round two");
        Require(!ChestRounds.Start(synchronizer).SecondRound, "The next chest must start on round one");
    }

    private static void CheckSimultaneousSlotRemoval()
    {
        foreach (int count in new[] { 4, 6, 8 })
        {
            var slots = new[] { 1, count - 1, 0 };
            var holders = Enumerable.Range(0, count).Select(slot =>
            {
                var holder = Uninitialized<NTreasureRoomRelicHolder>();
                holder.Index = slot;
                return holder;
            }).ToList();
            foreach (int slot in slots)
                Require(ChestScreen.RemoveByOriginalSlot(holders, slot).Index == slot, "Award removed the wrong holder");
            Require(holders.Select(holder => holder.Index).SequenceEqual(Enumerable.Range(0, count).Except(slots)),
                "Remaining holders must keep their order");
        }
    }

    private static void CheckFallbackRelicOrder()
    {
        var sync = Uninitialized<Synchronizer>();
        RelicModel fallback = Uninitialized<Circlet>();
        RelicModel other = Uninitialized<Anchor>();
        var pool = new List<RelicModel> { fallback, other, fallback };
        var capture = new ChestRounds.AwardSnapshot(sync, pool);
        pool.Remove(fallback);
        var eventRef = AccessTools.FieldRefAccess<Synchronizer, Action<List<RelicPickingResult>, bool>>(nameof(Synchronizer.RelicsAwarded));
        eventRef(sync).Invoke([new RelicPickingResult { relic = fallback, relicIndex = 2, player = null }], false);
        capture.Finish(apply: true);
        Require(pool.Count == 2 && ReferenceEquals(pool[0], fallback) && ReferenceEquals(pool[1], other),
            "Repeated fallback relics must be removed by original slot");
        Require(eventRef(sync) is null, "The award observer must detach after use");
    }

    private static T Uninitialized<T>() where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
