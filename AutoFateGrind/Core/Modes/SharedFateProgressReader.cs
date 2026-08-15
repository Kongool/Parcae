using AutoFateGrind.Core.Trading;
using AutoFateGrind.Core.Zones;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AutoFateGrind.Core.Modes;

public readonly record struct SharedFateSnapshot(
    uint TerritoryId,
    byte CurrentRank,
    byte MaxRank,
    ushort FateProgress,
    ushort NeededFates)
{
    public bool IsComplete => MaxRank > 0 && CurrentRank >= MaxRank;
}

// Reads the same three expansion tabs used by Travel > Shared FATE. A single refresh is requested
// when the mode is opened/started; progress earned by this run is then overlaid locally so the
// runner does not continuously poll the game server.
internal static unsafe class SharedFateProgressReader
{
    public static readonly ExpansionKind[] SupportedExpansions =
        [ExpansionKind.ShB, ExpansionKind.EW, ExpansionKind.DT];

    private static readonly long[] lastRequestAtMs = new long[SupportedExpansions.Length];
    private const int RequestCooldownMs = 30_000;

    public static bool IsSupported(ExpansionKind expansion)
        => expansion is ExpansionKind.ShB or ExpansionKind.EW or ExpansionKind.DT;

    public static IReadOnlyList<uint> ZoneIds(ExpansionKind expansion)
        => GemstoneTrader.Traders
            .Where(t => !t.IsHub && t.Expansion == expansion)
            .Select(t => t.TerritoryId)
            .Distinct()
            .ToArray();

    public static bool EnsureRequested(ExpansionKind expansion)
    {
        if (!TryTabIndex(expansion, out var tabIndex)) return false;
        if (HasLoadedExpansion(expansion)) return true;

        var now = Environment.TickCount64;
        if (now - lastRequestAtMs[tabIndex] < RequestCooldownMs) return false;
        return RequestRefresh(expansion);
    }

    public static bool RequestRefresh(ExpansionKind expansion)
    {
        if (!TryTabIndex(expansion, out var tabIndex)) return false;
        lastRequestAtMs[tabIndex] = Environment.TickCount64;

        var achievement = Achievement.Instance();
        return achievement is not null && achievement->RequestFateProgressTab(tabIndex);
    }

    public static bool HasLoadedExpansion(ExpansionKind expansion)
    {
        if (!TryTabIndex(expansion, out var tabIndex)) return false;
        var agent = AgentFateProgress.Instance();
        if (agent is null) return false;

        ref var tab = ref agent->Tabs[tabIndex];
        foreach (ref var zone in tab.Zones)
            if (zone.TerritoryTypeId != 0) return true;
        return false;
    }

    public static bool HasProgressFor(IEnumerable<ZoneInfo> zones)
        => zones.All(z => TryGetLive(z.Expansion, z.TerritoryId, out _));

    public static bool TryGetLiveSummary(ExpansionKind expansion, out int complete, out int total)
    {
        complete = 0;
        var known = 0;
        var ids = ZoneIds(expansion);
        total = ids.Count;
        foreach (var id in ids)
        {
            if (!TryGetLive(expansion, id, out var progress)) continue;
            known++;
            if (progress.IsComplete) complete++;
        }
        return total > 0 && known == total;
    }

    public static bool TryGetLive(ExpansionKind expansion, uint territoryId, out SharedFateSnapshot snapshot)
    {
        snapshot = default;
        if (!TryTabIndex(expansion, out var tabIndex)) return false;
        var agent = AgentFateProgress.Instance();
        if (agent is null) return false;

        ref var tab = ref agent->Tabs[tabIndex];
        foreach (ref var zone in tab.Zones)
        {
            if (zone.TerritoryTypeId != territoryId) continue;
            snapshot = new SharedFateSnapshot(
                zone.TerritoryTypeId,
                zone.CurrentRank,
                zone.MaxRank,
                zone.FateProgress,
                zone.NeededFates);
            return snapshot.MaxRank > 0;
        }
        return false;
    }

    public static bool TryGetEffective(
        ZoneInfo zone,
        IReadOnlyDictionary<uint, SharedFateSnapshot>? baseline,
        IReadOnlyDictionary<uint, int>? completedByZone,
        out SharedFateSnapshot snapshot)
    {
        snapshot = default;
        var found = baseline is not null && baseline.TryGetValue(zone.TerritoryId, out snapshot);
        if (!found && !TryGetLive(zone.Expansion, zone.TerritoryId, out snapshot)) return false;

        var earned = completedByZone?.GetValueOrDefault(zone.TerritoryId) ?? 0;
        snapshot = ApplyCompletions(snapshot, earned);
        return true;
    }

    public static SharedFateSnapshot ApplyCompletions(SharedFateSnapshot snapshot, int completions)
    {
        var rank = snapshot.CurrentRank;
        var progress = (int)snapshot.FateProgress;
        var needed = (int)snapshot.NeededFates;

        while (completions > 0 && snapshot.MaxRank > 0 && rank < snapshot.MaxRank)
        {
            if (needed <= 0) needed = NeededForRank(rank);
            if (needed <= 0) break;

            var leftAtRank = Math.Max(1, needed - progress);
            if (completions < leftAtRank)
            {
                progress += completions;
                completions = 0;
                break;
            }

            completions -= leftAtRank;
            rank++;
            progress = 0;
            needed = rank >= snapshot.MaxRank ? 0 : NeededForRank(rank);
        }

        return snapshot with
        {
            CurrentRank = rank,
            FateProgress = (ushort)Math.Clamp(progress, 0, ushort.MaxValue),
            NeededFates = (ushort)Math.Clamp(needed, 0, ushort.MaxValue),
        };
    }

    private static int NeededForRank(byte currentRank) => currentRank switch
    {
        1 => 6,
        2 => 60,
        _ => 0,
    };

    private static bool TryTabIndex(ExpansionKind expansion, out byte tabIndex)
    {
        tabIndex = expansion switch
        {
            ExpansionKind.ShB => 0,
            ExpansionKind.EW  => 1,
            ExpansionKind.DT  => 2,
            _                 => byte.MaxValue,
        };
        return tabIndex != byte.MaxValue;
    }
}
