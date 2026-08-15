using AutoFateGrind.Core.Modes;

namespace AutoFateGrind.Core.Zones;

internal static class ZoneSelection
{
    public static IReadOnlyList<ZoneInfo> ResolveStartList(Configuration cfg)
    {
        if (cfg.ActiveMode.Id == SharedFateCompletionMode.ModeId)
            return ResolveSharedFateStartList(cfg);

        var byId = ZoneRegistry.Zones.ToDictionary(z => z.TerritoryId);
        return cfg.SelectedZones.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
    }

    public static IReadOnlyList<ZoneInfo> SharedFateExpansionZones(ExpansionKind expansion)
    {
        var byId = ZoneRegistry.Zones.ToDictionary(z => z.TerritoryId);
        return SharedFateProgressReader.ZoneIds(expansion)
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .ToList();
    }

    private static IReadOnlyList<ZoneInfo> ResolveSharedFateStartList(Configuration cfg)
    {
        if (!SharedFateProgressReader.IsSupported(cfg.SharedFateExpansion)) return [];

        var candidates = SharedFateExpansionZones(cfg.SharedFateExpansion)
            .Where(zone =>
            {
                ZoneStateReader.Refresh(zone);
                return zone.Unlocked;
            })
            .Where(zone => !SharedFateProgressReader.TryGetLive(
                               zone.Expansion, zone.TerritoryId, out var progress)
                        || !progress.IsComplete)
            .ToList();

        if (cfg.SharedFateRotateZones || candidates.Count <= 1) return candidates;

        var current = candidates.FirstOrDefault(z => z.TerritoryId == ECommons.DalamudServices.Svc.ClientState.TerritoryType);
        return current is not null ? [current] : [candidates[0]];
    }
}
