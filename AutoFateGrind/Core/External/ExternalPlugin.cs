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
    Odysseus,
    TextAdvance,
}

// What a dependency does for Parcae; the Dependencies window groups rows by it.
public enum DependencyRole
{
    Movement,
    Combat,
    Skips,
}

public sealed record ExternalPluginInfo(
    string InternalName,
    string DisplayName,
    string RepoUrl,
    string Purpose,
    DependencyRole Role,
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
            Purpose: "Pathfinding and movement to FATEs.",
            Role: DependencyRole.Movement),
        [ExternalPlugin.Ariadne] = new(
            InternalName: "Ariadne",
            DisplayName: "Ariadne",
            RepoUrl: "https://github.com/ofnature/Ariadne",
            Purpose: "Pathfinding and movement to FATEs from cached navmeshes; serves the vnavmesh IPC when vnavmesh is not loaded.",
            Role: DependencyRole.Movement,
            OneClickInstall: false),
        [ExternalPlugin.BossMod] = new(
            InternalName: "BossMod",
            DisplayName: "BossMod",
            RepoUrl: "https://puni.sh/api/repository/veyn",
            Purpose: "Auto-rotation, targeting, and dodging during FATE combat.",
            Role: DependencyRole.Combat,
            Aliases: ["BossModReborn"]),
        [ExternalPlugin.Minerva] = new(
            InternalName: "Minerva",
            DisplayName: "Minerva",
            RepoUrl: "",
            Purpose: "Mechanic dodging during FATE combat through a Minerva preset (paired with Daedalus).",
            Role: DependencyRole.Combat,
            OneClickInstall: false),
        [ExternalPlugin.Daedalus] = new(
            InternalName: "Daedalus",
            DisplayName: "Daedalus",
            RepoUrl: "https://raw.githubusercontent.com/ofnature/Daedalus/main/repo.json",
            Purpose: "Combat rotation while Minerva dodges; Parcae keeps a FATE mob hard-targeted for it.",
            Role: DependencyRole.Combat),
        [ExternalPlugin.Odysseus] = new(
            InternalName: "Odysseus",
            DisplayName: "Odysseus",
            RepoUrl: "https://raw.githubusercontent.com/ofnature/Odysseus/main/repo.json",
            Purpose: "Clicks through NPC dialogue and fills and hands over the Collect FATE turn-in window, on a lease Parcae holds only while it needs it. Preferred over TextAdvance.",
            Role: DependencyRole.Skips),
        [ExternalPlugin.TextAdvance] = new(
            InternalName: "TextAdvance",
            DisplayName: "TextAdvance",
            RepoUrl: "https://raw.githubusercontent.com/NightmareXIV/MyDalamudPlugins/main/pluginmaster.json",
            Purpose: "Fallback when Odysseus is absent: talk skip and Collect turn-ins, plus the Bicolor trader's dialogue.",
            Role: DependencyRole.Skips),
    };

    public static IEnumerable<ExternalPlugin> All => Catalog.Keys;

    public static IEnumerable<ExternalPlugin> ByRole(DependencyRole role) => All.Where(p => Catalog[p].Role == role);

    // Combat and navigation each have two interchangeable providers; only the selected one is required.
    public static bool IsRequired(ExternalPlugin plugin) => plugin switch
    {
        ExternalPlugin.Vnavmesh => Plugin.Cfg.NavigationPlugin == NavigationPlugin.Vnavmesh,
        ExternalPlugin.Ariadne  => Plugin.Cfg.NavigationPlugin == NavigationPlugin.Ariadne,
        ExternalPlugin.BossMod  => Plugin.Cfg.CombatPlugin == CombatPlugin.BossMod,
        ExternalPlugin.Minerva  => Plugin.Cfg.CombatPlugin == CombatPlugin.Minerva,
        ExternalPlugin.Daedalus => Plugin.Cfg.CombatPlugin == CombatPlugin.Minerva,
        // Skips only smooth Collect turn-ins and auto-trade dialogue; either provider (or neither) works.
        ExternalPlugin.Odysseus    => false,
        ExternalPlugin.TextAdvance => false,
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
           && !TextAdvanceIPC.IsPluginEnabled()
           && !OdysseusIPC.IsAvailable;
}
