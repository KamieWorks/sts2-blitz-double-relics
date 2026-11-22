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
    internal const string LogPrefix = "[BlitzDoubleRelics]";

    public static void Initialize()
    {
        string? version = ReleaseInfoManager.Instance.ReleaseInfo?.Version;
        if (version != SupportedVersion)
        {
            Log.Warn($"{LogPrefix} Unsupported game version {version ?? "unknown"}; expected {SupportedVersion}.");
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
