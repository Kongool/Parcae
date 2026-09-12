using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;
using ECommons.Throttlers;

namespace AutoFateGrind.Core.Ipc;

// Daedalus registers the RotationSolverReborn-compatible gates (whenever real RSR is absent) and runs its
// rotation while the external-combat override is held. It never pulls on its own: with the override on
// it opens on whatever valid hard target the driver set, so AutoFate.EnsureFateTarget keeps a live FATE
// mob targeted and RecordTargetWrite attributes that write to automation.
internal sealed class DaedalusIPC
{
    private static DaedalusIPC? instance;
    public static DaedalusIPC Instance => instance ??= new DaedalusIPC();

    // Mirror of RSR's StateCommandType; numeric values must match (Off=0, Auto=1, TargetOnly=2, Manual=3, AutoDuty=4, Henched=5).
    private enum StateCommandType : byte { Off, Auto, TargetOnly, Manual, AutoDuty, Henched }

    private const string ReassertKey = "AFG.Daedalus.Reassert";
    private const int ReassertMs = 2000;

    private readonly ICallGateSubscriber<bool>                     isEnabled;
    private readonly ICallGateSubscriber<StateCommandType, object> changeOperatingMode;
    private readonly ICallGateSubscriber<ulong, object>            recordExternalTargetWrite;
    private bool engaged;

    private DaedalusIPC()
    {
        isEnabled                 = Svc.PluginInterface.GetIpcSubscriber<bool>("Daedalus.IsEnabled");
        changeOperatingMode       = Svc.PluginInterface.GetIpcSubscriber<StateCommandType, object>("RotationSolverReborn.ChangeOperatingMode");
        recordExternalTargetWrite = Svc.PluginInterface.GetIpcSubscriber<ulong, object>("Daedalus.Targeting.RecordExternalWrite");
    }

    public bool IsAvailable => isEnabled.HasFunction && changeOperatingMode.HasFunction;

    // Called every engage tick. Sends Manual once, then re-asserts every few seconds so another driver's
    // fight-end Off (Questionable shares this gate) can't strand a run mid-FATE.
    public void Engage()
    {
        if (engaged && !EzThrottler.Throttle(ReassertKey, ReassertMs)) return;
        engaged = true;
        IpcGate.Run(changeOperatingMode.HasFunction, () => changeOperatingMode.InvokeAction(StateCommandType.Manual), "[DaedalusIPC] ChangeOperatingMode(Manual) failed");
    }

    public void Release()
    {
        if (!engaged) return;
        engaged = false;
        IpcGate.Run(changeOperatingMode.HasFunction, () => changeOperatingMode.InvokeAction(StateCommandType.Off), "[DaedalusIPC] ChangeOperatingMode(Off) failed");
    }

    public void RecordTargetWrite(ulong gameObjectId)
        => IpcGate.Run(recordExternalTargetWrite.HasFunction, () => recordExternalTargetWrite.InvokeAction(gameObjectId), "[DaedalusIPC] RecordExternalWrite failed");
}
