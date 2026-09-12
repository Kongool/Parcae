using AutoFateGrind.Core.Ipc;
using ECommons.DalamudServices;

namespace AutoFateGrind.Core.External;

public enum ExternalPlugin
{
    Vnavmesh,
    Ariadne,
    BossMod,
    Minerva,
    Daedalus,
    TextAdvance,
}

public sealed record ExternalPluginInfo(
    string InternalName,
    string DisplayName,
    string RepoUrl,
    string Purpose,
    // Alternate InternalNames (community forks with the same IPC surface).
    string[]? Aliases = null,
    // False when there is no plugin repository to add (dev builds loaded via Dev Plugin Locations).
    bool OneClickInstall = true);

public static class ExternalPlugins
{
    public static readonly IReadOnlyDictionary<ExternalPlugin, ExternalPluginInfo> Catalog
        = new Dictionary<ExternalPlugin, ExternalPluginInfo>
    {
        [ExternalPlugin.Vnavmesh] = new(
            InternalName: "vnavmesh",
            DisplayName: "vnavmesh",
            RepoUrl: "https://puni.sh/api/repository/veyn",
            Purpose: "Pathfinding and movement to FATEs."),
        [ExternalPlugin.Ariadne] = new(
            InternalName: "Ariadne",
            DisplayName: "Ariadne",
            RepoUrl: "https://github.com/ofnature/Ariadne",
            Purpose: "Pathfinding and movement to FATEs from cached navmeshes; serves the vnavmesh IPC when vnavmesh is not loaded.",
            OneClickInstall: false),
        [ExternalPlugin.BossMod] = new(
            InternalName: "BossMod",
            DisplayName: "BossMod",
            RepoUrl: "https://puni.sh/api/repository/veyn",
            Purpose: "Auto-rotation, targeting, and dodging during FATE combat.",
            Aliases: ["BossModReborn"]),
        [ExternalPlugin.Minerva] = new(
            InternalName: "Minerva",
            DisplayName: "Minerva",
            RepoUrl: "",
            Purpose: "Mechanic dodging during FATE combat through a Minerva preset (paired with Daedalus).",
            OneClickInstall: false),
        [ExternalPlugin.Daedalus] = new(
            InternalName: "Daedalus",
            DisplayName: "Daedalus",
            RepoUrl: "https://raw.githubusercontent.com/ofnature/Daedalus/main/repo.json",
            Purpose: "Combat rotation while Minerva dodges; Parcae keeps a FATE mob hard-targeted for it."),
        [ExternalPlugin.TextAdvance] = new(
            InternalName: "TextAdvance",
            DisplayName: "TextAdvance",
            RepoUrl: "https://raw.githubusercontent.com/NightmareXIV/MyDalamudPlugins/main/pluginmaster.json",
            Purpose: "Talk-skip during Collect FATE turn-ins (scoped, only enabled mid-Collect)."),
    };

    public static IEnumerable<ExternalPlugin> All => Catalog.Keys;

    // Combat and navigation each have two interchangeable providers; only the selected one is required.
    public static bool IsRequired(ExternalPlugin plugin) => plugin switch
    {
        ExternalPlugin.Vnavmesh => Plugin.Cfg.NavigationPlugin == NavigationPlugin.Vnavmesh,
        ExternalPlugin.Ariadne  => Plugin.Cfg.NavigationPlugin == NavigationPlugin.Ariadne,
        ExternalPlugin.BossMod  => Plugin.Cfg.CombatPlugin == CombatPlugin.BossMod,
        ExternalPlugin.Minerva  => Plugin.Cfg.CombatPlugin == CombatPlugin.Minerva,
        ExternalPlugin.Daedalus => Plugin.Cfg.CombatPlugin == CombatPlugin.Minerva,
        _ => true,
    };

    public static bool IsInstalled(ExternalPlugin plugin)
    {
        var info = Catalog[plugin];
        return Svc.PluginInterface.InstalledPlugins.Any(p =>
            p.IsLoaded
            && (p.InternalName == info.InternalName
                || (info.Aliases is not null && Array.IndexOf(info.Aliases, p.InternalName) >= 0)));
    }

    public static IEnumerable<ExternalPlugin> MissingRequired()
        => All.Where(p => IsRequired(p) && !IsInstalled(p));

    public static bool AllRequiredInstalled() => !MissingRequired().Any();

    // Loaded in Dalamud but its own in-plugin toggle is off. Advisory only — AFG drives
    // TextAdvance via external control, so this never gates the grind, it only warns.
    public static bool IsInstalledButDisabled(ExternalPlugin plugin)
        => plugin == ExternalPlugin.TextAdvance
           && IsInstalled(plugin)
           && !TextAdvanceIPC.IsPluginEnabled();
}
