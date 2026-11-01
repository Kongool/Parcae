using AutoFateGrind.Core.External;
using ECommons.DalamudServices;
using ECommons.Throttlers;

namespace AutoFateGrind.Core.Ipc;

// One owner for "click through NPC dialogue for me" across Collect FATE turn-ins and auto-trade. Odysseus's
// assist lease is preferred: a TTL that also fills and hands over the Request window, and lapses on its own
// if a run dies. TextAdvance's external control is the fallback when Odysseus is absent or older.
internal static class SkipAssist
{
    private const string TextAdvanceScope = AfgConstants.TextAdvanceCallerName;
    private const string RefreshKey = "AFG.SkipAssist.Refresh";

    private static bool odysseusLeased;
    private static bool textAdvanceArmed;
    private static int leaseSeconds;

    public static bool Armed => odysseusLeased || textAdvanceArmed;

    // handOver: also fill and hand over the Request window. Odysseus's lease always covers both; it only
    // narrows what the TextAdvance fallback is asked to do.
    public static void Arm(string purpose, int seconds, bool handOver)
    {
        if (Armed) return;

        if (OdysseusIPC.IsAvailable && OdysseusIPC.Lease(seconds))
        {
            odysseusLeased = true;
            leaseSeconds = seconds;
            EzThrottler.Throttle(RefreshKey, RefreshIntervalMs(seconds), rethrottle: true);
            Svc.Log.Info($"{AfgConstants.LogPrefix} {purpose}: dialogue assist via Odysseus lease ({seconds}s)");
            return;
        }

        if (!ExternalPlugins.IsInstalled(ExternalPlugin.TextAdvance)) return;
        try
        {
            TextAdvanceIPC.EnableExternalControl(TextAdvanceScope, talkSkip: true, requestFill: handOver, requestHandin: handOver);
            textAdvanceArmed = true;
            Svc.Log.Info($"{AfgConstants.LogPrefix} {purpose}: dialogue assist via TextAdvance");
        }
        catch (Exception ex)
        {
            Svc.Log.Warning(ex, "[AFG] TextAdvance enable failed");
        }
    }

    // Safe to call every tick; renews the Odysseus lease at half its TTL.
    public static void Refresh()
    {
        if (!odysseusLeased) return;
        if (EzThrottler.Throttle(RefreshKey, RefreshIntervalMs(leaseSeconds)))
            OdysseusIPC.Lease(leaseSeconds);
    }

    public static void Release()
    {
        if (odysseusLeased)
        {
            OdysseusIPC.Lease(0);
            odysseusLeased = false;
        }
        if (!textAdvanceArmed) return;
        try { TextAdvanceIPC.DisableExternalControl(TextAdvanceScope); }
        catch (Exception ex) { Svc.Log.Warning(ex, "[AFG] TextAdvance disable failed"); }
        textAdvanceArmed = false;
    }

    private static int RefreshIntervalMs(int seconds) => Math.Max(5, seconds / 2) * 1000;
}
