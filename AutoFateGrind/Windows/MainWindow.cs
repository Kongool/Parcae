using AutoFateGrind.Core.External;
using AutoFateGrind.Core.Modes;
using AutoFateGrind.Core.Tasks;
using AutoFateGrind.Core.Zones;
using AutoFateGrind.Windows.Components;
using AutoFateGrind.Windows.Sections;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System.Numerics;

namespace AutoFateGrind.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    public MainWindow(Plugin plugin) : base("Parcae — FATE Operations###ParcaeMain")
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(100, 100),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        Size = new Vector2(820, 760);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var cfg = plugin.Configuration;
        var ctrl = plugin.Controller;

        using var style = Styling.PushWindowStyle();

        ParcaeHeader.Draw(plugin, ctrl);
        Styling.VSpace(7f);
        DependencyBanner.Draw(plugin);

        if (ctrl.Running) RunningPanel.Draw(cfg, ctrl);
        else              DrawIdle(cfg, ctrl);
    }

    private void DrawIdle(Configuration cfg, AutoFateController ctrl)
    {
        IdleHeader.Draw(cfg, plugin);
        ImGui.Spacing();

        var zoneCount = ZoneSelection.ResolveStartList(cfg).Count;
        var sharedFates = cfg.ActiveMode.Id == SharedFateCompletionMode.ModeId;
        StepHeader.Draw(1, sharedFates ? "Choose the expansion route" : "Shape the route",
            zoneCount > 0
                ? sharedFates
                    ? cfg.SharedFateRotateZones ? $"{zoneCount} remaining" : "single zone"
                    : $"{zoneCount} selected"
                : null);
        ZonePicker.Draw(cfg, ctrl);

        ImGui.Spacing();
        StepHeader.Draw(2, "Choose the thread's end");
        GoalSummary.Draw(cfg);

        ImGui.Spacing();
        StepHeader.Draw(3, "Spend the spoils", GemstonePurchaseSummary.HeaderSummary(cfg));
        GemstonePurchaseSummary.Draw(cfg);

        ImGui.Spacing();
        ImGui.Spacing();
        DrawStart(cfg, ctrl);
    }

    private static void DrawStart(Configuration cfg, AutoFateController ctrl)
    {
        var startList = ZoneSelection.ResolveStartList(cfg);
        var depsOk = ExternalPlugins.AllRequiredInstalled();
        var canStart = startList.Count > 0 && !ctrl.Running && depsOk;
        var reason = !depsOk ? "install required plugins"
            : startList.Count == 0 && cfg.ActiveMode.Id == SharedFateCompletionMode.ModeId
                ? "no unfinished unlocked zones in this expansion"
            : startList.Count == 0 ? "pick at least one zone below"
            : "";
        var sub = $"{startList.Count} zone{(startList.Count == 1 ? "" : "s")}";

        if (StartButton.Draw(sub, canStart, reason))
            ctrl.RunAll(startList);
    }
}
