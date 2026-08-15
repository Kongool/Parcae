using AutoFateGrind.Core.Modes;
using AutoFateGrind.Core.Tasks;
using AutoFateGrind.Core.Trading;
using AutoFateGrind.Core.Zones;

namespace AutoFateGrind.Windows.Sections;

// Resolves the active stop-condition into the goal ring's display: a 0..1 fraction (null = no finish line),
// the big/small center text, and a short remaining phrase shared by the hero card and the ELAPSED stat tile.
internal static class GoalProgress
{
    public readonly record struct Info(float? Fraction, string CenterBig, string CenterSmall, string Remaining, bool Endless);

    public static Info Resolve(Configuration cfg, AutoFateSession? s)
    {
        var completed = s?.CompletedCount ?? 0;

        switch (cfg.ActiveMode.Id)
        {
            case MaxGemstonesMode.ModeId:
            {
                var have = s?.GemstoneCurrent ?? GemstoneCatalog.CurrentWalletCount();
                var target = Math.Max(1, cfg.TargetGemstoneCount);
                var left = Math.Max(0, target - have);
                return new Info(
                    Math.Clamp(have / (float)target, 0f, 1f),
                    have.ToString(), $"/ {target}",
                    left > 0 ? $"{left} gems to go" : "target reached", false);
            }
            case RunCountMode.ModeId:
            {
                var target = Math.Max(1, cfg.TargetFateCount);
                var left = Math.Max(0, target - completed);
                return new Info(
                    Math.Clamp(completed / (float)target, 0f, 1f),
                    completed.ToString(), $"/ {target}",
                    left > 0 ? $"{left} FATEs left" : "target reached", false);
            }
            case SharedFateCompletionMode.ModeId:
            {
                if (!cfg.SharedFateRotateZones)
                {
                    var target = s?.SharedFateBaseline.Keys
                        .Select(id => ZoneRegistry.Zones.FirstOrDefault(z => z.TerritoryId == id))
                        .FirstOrDefault(z => z is not null)
                        ?? ZoneSelection.ResolveStartList(cfg).FirstOrDefault();
                    if (target is null || !SharedFateProgressReader.TryGetEffective(
                            target, s?.SharedFateBaseline, s?.CompletedByZone, out var rank))
                        return new Info(0f, "...", "rank", "loading Shared FATE progress", false);

                    const int totalFates = 66;
                    var overall = rank.CurrentRank switch
                    {
                        >= 3 => totalFates,
                        2    => 6 + rank.FateProgress,
                        _    => rank.FateProgress,
                    };
                    var remainingAtRank = Math.Max(0, rank.NeededFates - rank.FateProgress);
                    return new Info(
                        Math.Clamp(overall / (float)totalFates, 0f, 1f),
                        $"R{rank.CurrentRank}", $"/ R{rank.MaxRank}",
                        rank.IsComplete
                            ? $"{target.Name} complete"
                            : $"{remainingAtRank} FATEs to next rank", false);
                }

                var zones = ZoneSelection.SharedFateExpansionZones(cfg.SharedFateExpansion);
                var total = zones.Count;
                var done = 0;
                var known = 0;
                foreach (var zone in zones)
                {
                    if (!SharedFateProgressReader.TryGetEffective(
                            zone, s?.SharedFateBaseline, s?.CompletedByZone, out var progress))
                        continue;
                    known++;
                    if (progress.IsComplete) done++;
                }

                if (total == 0 || known == 0)
                    return new Info(0f, "...", "zones", "loading Shared FATE progress", false);

                var left = Math.Max(0, total - done);
                return new Info(
                    Math.Clamp(done / (float)total, 0f, 1f),
                    done.ToString(), $"/ {total}",
                    left > 0 ? $"{left} zone{(left == 1 ? "" : "s")} left" : "expansion complete", false);
            }
            case TimeBoxedMode.ModeId:
            {
                var targetMin = Math.Max(1, cfg.TargetMinutes);
                var elapsed = s?.Elapsed ?? TimeSpan.Zero;
                var remaining = TimeSpan.FromMinutes(targetMin) - elapsed;
                var rem = remaining > TimeSpan.Zero
                    ? remaining.TotalHours >= 1
                        ? $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}m left"
                        : $"{remaining.Minutes}m {remaining.Seconds:D2}s left"
                    : "time reached";
                return new Info(
                    Math.Clamp((float)(elapsed.TotalMinutes / targetMin), 0f, 1f),
                    $"{(int)elapsed.TotalMinutes}m", $"/ {targetMin}m",
                    rem, false);
            }
            default:
                return new Info(null, completed.ToString(), "done", "until you stop", true);
        }
    }
}
