using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;

namespace AutoFateGrind.Core.Ipc;

// Odysseus lends its dialogue and Request hand-over handling to other plugins through a TTL lease: while a
// lease is live it advances Talk boxes and fills and hands over the Request window. A caller that stops
// refreshing (or crashes) simply lets the lease expire, so the UI can never stay hijacked.
internal static class OdysseusIPC
{
    private const string Caller = "Parcae";

    private static ICallGateSubscriber<string, int, bool>? lease;
    private static bool initialized;

    private static void EnsureInit()
    {
        if (initialized) return;
        initialized = true;
        lease = Svc.PluginInterface.GetIpcSubscriber<string, int, bool>("Odysseus.Assist.Lease");
    }

    public static bool IsAvailable
    {
        get
        {
            EnsureInit();
            return lease!.HasFunction;
        }
    }

    // Force the assist on for `seconds` (Odysseus caps at 300); 0 releases Parcae's lease.
    public static bool Lease(int seconds)
    {
        EnsureInit();
        return IpcGate.Invoke(lease!.HasFunction, () => lease.InvokeFunc(Caller, seconds), false, "[OdysseusIPC] Assist.Lease failed");
    }
}
