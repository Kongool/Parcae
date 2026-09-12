using AutoFateGrind.Core.Ipc;
using AutoFateGrind.Windows.Components;

namespace AutoFateGrind.Windows.Sections.Config;

internal static class GeneralSettings
{
    private static readonly CombatPlugin[] combatPlugins = [CombatPlugin.BossMod, CombatPlugin.Minerva];
    private static readonly SettingsControls.Choices.Choice[] combatChoices =
    [
        new("BossMod", "BossMod or BossMod Reborn: auto-rotation, auto-target, dodging, and obstacle maps through the bundled Parcae AI preset."),
        new("Minerva + Daedalus", "Minerva dodges mechanics through the preset you pick; Daedalus runs the rotation while Parcae keeps a FATE mob hard-targeted for it. Both are required."),
    ];

    private static readonly NavigationPlugin[] navigationPlugins = [NavigationPlugin.Vnavmesh, NavigationPlugin.Ariadne];
    private static readonly SettingsControls.Choices.Choice[] navigationChoices =
    [
        new("vnavmesh", "Builds the navmesh in-game and drives all pathing and movement."),
        new("Ariadne", "Cached navmeshes from Mnemosyne. Ariadne answers the vnavmesh IPC when vnavmesh is not loaded, so movement works unchanged."),
    ];

    public static void Draw(Configuration cfg)
    {
        DrawWindowGroup(cfg);
        DrawBehaviorGroup(cfg);
        DrawPluginsGroup(cfg);
    }

    private static void DrawWindowGroup(Configuration cfg)
    {
        using var group = SettingsGroup.Begin("Window");

        SettingsRow.Draw("Open on login",
            "Pop the main window automatically the next time you log in.",
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(cfg, () => cfg.AutoShowOnLogin, v => cfg.AutoShowOnLogin = v, "##gen_autoshow"),
            SettingsRow.ToggleHeight);

        SettingsRow.Draw("Live FATE tracker popout",
            "Show the live FATE tracker as a small overlay window so you can keep it visible while the main window is closed.",
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(cfg, () => cfg.ShowLivePopout, v =>
            {
                cfg.ShowLivePopout = v;
                Plugin.Instance.LiveFateWindow.IsOpen = v;
            }, "##gen_popout"),
            SettingsRow.ToggleHeight);
    }

    private static void DrawBehaviorGroup(Configuration cfg)
    {
        using var group = SettingsGroup.Begin("Behavior");

        SettingsRow.Draw("Swap zones when empty",
            "For gemstone, count, time, and endless modes: when the current zone runs out of eligible FATEs, jump to the next zone in your priority order. Shared FATE mode uses its own Move between zones option.",
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(cfg, () => cfg.SwapZonesWhenEmpty, v => cfg.SwapZonesWhenEmpty = v, "##gen_swap"),
            SettingsRow.ToggleHeight);

        SettingsRow.Draw("Auto-pause in content",
            "Pause the run while you are inside a duty, trial, raid, or any other instanced content, then resume it once you are back outside. Your zones, goal, and session stats are kept, and paused time does not count toward a time-based goal.",
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(cfg, () => cfg.AutoPauseInContent, v => cfg.AutoPauseInContent = v, "##gen_autopause"),
            SettingsRow.ToggleHeight);

        SettingsRow.Draw("Auto-resume on fault",
            "If the grind hits an unrecoverable error and stops, automatically restart it (up to 3 times in 5 minutes) instead of ending the run. Leave off if you want faults to surface.",
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(cfg, () => cfg.AutoResumeOnFault, v => cfg.AutoResumeOnFault = v, "##gen_autoresume"),
            SettingsRow.ToggleHeight);
    }

    private static void DrawPluginsGroup(Configuration cfg)
    {
        using var group = SettingsGroup.Begin("Plugins");

        var combatSelected = Math.Max(0, Array.IndexOf(combatPlugins, cfg.CombatPlugin));
        SettingsRow.Draw("Combat plugin",
            "Which plugin Parcae hands combat to when a FATE is engaged. Only the selected one counts as a required dependency. Takes effect on the next engagement.",
            SettingsControls.RowComboWidth,
            () => SettingsControls.Choices.DrawCombo("##gen_combat", combatChoices, combatSelected, choice =>
            {
                cfg.CombatPlugin = combatPlugins[choice];
                cfg.SaveDebounced();
            }));
        SettingsRow.Caption(combatChoices[combatSelected].Detail);

        if (cfg.CombatPlugin == CombatPlugin.Minerva)
            DrawMinervaPreset(cfg);

        var navSelected = Math.Max(0, Array.IndexOf(navigationPlugins, cfg.NavigationPlugin));
        SettingsRow.Draw("Navigation plugin",
            "Which plugin provides pathfinding and movement. Only the selected one counts as a required dependency.",
            SettingsControls.RowComboWidth,
            () => SettingsControls.Choices.DrawCombo("##gen_nav", navigationChoices, navSelected, choice =>
            {
                cfg.NavigationPlugin = navigationPlugins[choice];
                cfg.SaveDebounced();
            }));
        SettingsRow.Caption(navigationChoices[navSelected].Detail);
    }

    private static void DrawMinervaPreset(Configuration cfg)
    {
        var presets = MinervaIPC.Instance.IsAvailable ? MinervaIPC.Instance.ListPresets() : [];
        if (presets.Length == 0)
        {
            SettingsRow.Note($"Minerva is not loaded. Preset \"{cfg.MinervaPresetName}\" will be applied once it is.");
            return;
        }

        var choices = presets.Select(p => new SettingsControls.Choices.Choice(p, "")).ToArray();
        var selected = Math.Max(0, Array.IndexOf(presets, cfg.MinervaPresetName));
        SettingsRow.Draw("Minerva preset",
            "Minerva dodge preset Parcae claims while fighting a FATE and releases afterwards. Create and tune presets in Minerva; \"Default\" always exists.",
            SettingsControls.RowComboWidth,
            () => SettingsControls.Choices.DrawCombo("##gen_minerva_preset", choices, selected, choice =>
            {
                cfg.MinervaPresetName = presets[choice];
                cfg.SaveDebounced();
            }));
    }
}
