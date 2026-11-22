using HarmonyLib;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace BlitzDoubleRelics;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    internal const string HarmonyId = "blitz-double-relics.multiplayer-treasure";
    internal const string SupportedVersion = "v0.113.1";
    internal const string SupportedCommit = "d09d48e5";

    internal static bool SupportsBuild(string? version, string? commit) =>
        version == SupportedVersion && commit == SupportedCommit;
    internal const string LogPrefix = "[BlitzDoubleRelics]";

    public static void Initialize()
    {
        ReleaseInfo? release = ReleaseInfoManager.Instance.ReleaseInfo;
        string? version = release?.Version;
        string? commit = release?.Commit;
        if (!SupportsBuild(version, commit))
        {
            Log.Info($"{LogPrefix} Inactive on {version ?? "unknown version"} ({commit ?? "unknown revision"}). Supported build: {SupportedVersion} ({SupportedCommit}).");
            return;
        }

        ApplyPatches();
        Version? modVersion = typeof(ModEntry).Assembly.GetName().Version;
        Log.Info($"{LogPrefix} {modVersion?.ToString(3)} active for multiplayer Blitz on {version}.");
    }

    internal static void ApplyPatches()
    {
        var harmony = new Harmony(HarmonyId);
        try
        {
            harmony.PatchAll(typeof(ModEntry).Assembly);
        }
        catch
        {
            harmony.UnpatchAll(HarmonyId);
            throw;
        }
    }
}
