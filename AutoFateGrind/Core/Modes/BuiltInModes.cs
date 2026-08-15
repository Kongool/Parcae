using AutoFateGrind.Core.Trading;

namespace AutoFateGrind.Core.Modes;

public sealed class EndlessMode : IFateGrindMode
{
    public const string ModeId = "endless";
    public string Id => ModeId;
    public string DisplayName => "Endless Grind";
    public string Description => "Runs forever, rotating between selected zones, until you press Stop.";
    public bool IsComplete(ModeContext ctx) => false;
    public string? GetRemainingDisplay(ModeContext ctx) => null;
}

public sealed class MaxGemstonesMode : IFateGrindMode
{
    public const string ModeId = "maxgemstones";
    public string Id => ModeId;
    public string DisplayName => "Farm Gemstones";
    public string Description => "Stops when Bicolor Gemstones hit your target. Auto-trade resumes the grind.";
    public bool IsComplete(ModeContext ctx) => GemstoneCatalog.CurrentWalletCount() >= Plugin.Cfg.TargetGemstoneCount;

    public string? GetRemainingDisplay(ModeContext ctx)
    {
        var have = GemstoneCatalog.CurrentWalletCount();
        var target = Plugin.Cfg.TargetGemstoneCount;
        return have < target ? $"{have} / {target} gems" : null;
    }
}

public sealed class SharedFateCompletionMode : IFateGrindMode
{
    public const string ModeId = "sharedfates";
    public string Id => ModeId;
    public string DisplayName => "Complete Shared FATEs";
    public string Description => "Finishes Shared FATE ranks in one zone or rotates through every unfinished zone in an expansion.";

    public bool IsComplete(ModeContext ctx)
    {
        if (ctx.Zones.Count == 0) return false;
        foreach (var zone in ctx.Zones)
        {
            if (!SharedFateProgressReader.TryGetEffective(
                    zone, ctx.SharedFateBaseline, ctx.CompletedByZone, out var progress)
             || !progress.IsComplete)
                return false;
        }
        return true;
    }

    public string? GetRemainingDisplay(ModeContext ctx)
    {
        var remaining = ctx.Zones.Count(zone =>
            !SharedFateProgressReader.TryGetEffective(
                zone, ctx.SharedFateBaseline, ctx.CompletedByZone, out var progress)
            || !progress.IsComplete);
        return remaining > 0 ? $"{remaining} zone{(remaining == 1 ? "" : "s")} left" : null;
    }
}

public sealed class TimeBoxedMode : IFateGrindMode
{
    public const string ModeId = "timeboxed";
    public string Id => ModeId;
    public string DisplayName => "Farm for Time";
    public string Description => "Runs for a set number of minutes, then stops. Always finishes the FATE in progress first.";
    public bool IsComplete(ModeContext ctx) => ctx.Elapsed >= TimeSpan.FromMinutes(Math.Max(1, Plugin.Cfg.TargetMinutes));

    public string? GetRemainingDisplay(ModeContext ctx)
    {
        var remaining = TimeSpan.FromMinutes(Math.Max(1, Plugin.Cfg.TargetMinutes)) - ctx.Elapsed;
        if (remaining <= TimeSpan.Zero) return null;
        return remaining.TotalHours >= 1
            ? $"{(int)remaining.TotalHours}h {remaining.Minutes:D2}m left"
            : $"{remaining.Minutes}m {remaining.Seconds:D2}s left";
    }
}

public sealed class RunCountMode : IFateGrindMode
{
    public const string ModeId = "runcount";
    public string Id => ModeId;
    public string DisplayName => "Run N FATEs";
    public string Description => "Stops after a fixed number of FATE completions across all selected zones.";
    public bool IsComplete(ModeContext ctx) => ctx.CompletedCount >= Plugin.Cfg.TargetFateCount;

    public string? GetRemainingDisplay(ModeContext ctx)
    {
        var remaining = Math.Max(0, Plugin.Cfg.TargetFateCount - ctx.CompletedCount);
        return remaining > 0 ? $"{remaining} FATEs left" : null;
    }
}
