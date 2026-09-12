using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;

namespace AutoFateGrind.Core.Ipc;

// Minerva keeps one active dodge-preset slot that another plugin may claim by owner name and must hand
// back. Only the holder can release it, so Parcae never undoes a preset the user applied themselves.
internal sealed class MinervaIPC
{
    private static MinervaIPC? instance;
    public static MinervaIPC Instance => instance ??= new MinervaIPC();

    public const string Owner = "Parcae";
    public const string DefaultPresetName = "Default";

    private readonly ICallGateSubscriber<bool>                 isConnected;
    private readonly ICallGateSubscriber<string[]>             listPresets;
    private readonly ICallGateSubscriber<string>               activePreset;
    private readonly ICallGateSubscriber<string, string, bool> applyPreset;
    private readonly ICallGateSubscriber<string, bool>         releasePreset;

    private MinervaIPC()
    {
        isConnected   = Svc.PluginInterface.GetIpcSubscriber<bool>("Minerva.IsConnected");
        listPresets   = Svc.PluginInterface.GetIpcSubscriber<string[]>("Minerva.ListPresets");
        activePreset  = Svc.PluginInterface.GetIpcSubscriber<string>("Minerva.ActivePreset");
        applyPreset   = Svc.PluginInterface.GetIpcSubscriber<string, string, bool>("Minerva.ApplyPreset");
        releasePreset = Svc.PluginInterface.GetIpcSubscriber<string, bool>("Minerva.ReleasePreset");
    }

    public bool IsAvailable => isConnected.HasFunction;

    public string[] ListPresets()
        => IpcGate.Invoke(listPresets.HasFunction, listPresets.InvokeFunc, Array.Empty<string>(), "[MinervaIPC] ListPresets failed");

    public string? GetActive()
        => IpcGate.Invoke<string?>(activePreset.HasFunction, activePreset.InvokeFunc, null, "[MinervaIPC] ActivePreset failed");

    // False when Minerva has no preset by that name.
    public bool Apply(string preset)
        => IpcGate.Invoke(applyPreset.HasFunction, () => applyPreset.InvokeFunc(preset, Owner), false, "[MinervaIPC] ApplyPreset failed");

    // No-op (false) unless Parcae currently holds the slot.
    public bool Release()
        => IpcGate.Invoke(releasePreset.HasFunction, () => releasePreset.InvokeFunc(Owner), false, "[MinervaIPC] ReleasePreset failed");
}
