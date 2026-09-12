using AutoFateGrind.Core.External;

namespace AutoFateGrind.Core.Ipc;

// Routes the combat handshake to whichever combat plugin the user selected: BossMod's AI preset, or the
// Minerva (dodging) + Daedalus (rotation) pair. BossMod-only extras (obstacle maps, the MaxTargets
// transient strategy) stay at their call sites behind UsesBossMod.
internal static class CombatIPC
{
    public static bool UsesBossMod => Plugin.Cfg.CombatPlugin == CombatPlugin.BossMod;

    public static string DisplayName => UsesBossMod ? "BossMod (or BossMod Reborn)" : "Minerva + Daedalus";

    public static bool IsAvailable => UsesBossMod
        ? BossModIPC.Instance.IsAvailable
        : MinervaIPC.Instance.IsAvailable && DaedalusIPC.Instance.IsAvailable;

    public static bool IsInstalled => UsesBossMod
        ? ExternalPlugins.IsInstalled(ExternalPlugin.BossMod)
        : ExternalPlugins.IsInstalled(ExternalPlugin.Minerva) && ExternalPlugins.IsInstalled(ExternalPlugin.Daedalus);

    public static string PresetName => UsesBossMod ? Plugin.Cfg.CombatPresetName : Plugin.Cfg.MinervaPresetName;

    public static void ClearActive()
    {
        if (UsesBossMod)
        {
            BossModIPC.Instance.ClearActive();
            return;
        }

        MinervaIPC.Instance.Release();
        DaedalusIPC.Instance.Release();
    }
}
