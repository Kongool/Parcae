using AutoFateGrind.Core.External;
using AutoFateGrind.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using System.Numerics;

namespace AutoFateGrind.Windows;

public sealed class DependenciesWindow : Window, IDisposable
{
    public DependenciesWindow() : base("Parcae — Dependencies###AutoFateGrindDeps")
    {
        Size = new Vector2(580, 540);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(100, 100),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        using var style = Styling.PushWindowStyle();

        DrawHeader();
        ImGui.Separator();
        ImGui.Spacing();
        DrawTable();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawFooter();
    }

    private static void DrawHeader()
    {
        ImGui.SetWindowFontScale(1.18f);
        using (ImRaii.PushColor(ImGuiCol.Text, Styling.TextStrong))
            ImGui.TextUnformatted("Plugins");
        ImGui.SetWindowFontScale(1.0f);

        var missing = ExternalPlugins.MissingRequired().Count();
        using (ImRaii.PushColor(ImGuiCol.Text, missing == 0 ? Styling.AccentMint : Styling.AccentRose))
            ImGui.TextUnformatted(missing == 0
                ? "All required plugins are installed and loaded."
                : $"{missing} required plugin{(missing == 1 ? " is" : "s are")} missing.");
    }

    private static void DrawTable()
    {
        if (!ImGui.BeginTable("##deps", 3,
                ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoBordersInBody | ImGuiTableFlags.PadOuterX | ImGuiTableFlags.RowBg))
            return;

        ImGui.TableSetupColumn("##status", ImGuiTableColumnFlags.WidthFixed, 32f * ImGuiHelpers.GlobalScale);
        ImGui.TableSetupColumn("##name", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##action", ImGuiTableColumnFlags.WidthFixed, 130f * ImGuiHelpers.GlobalScale);

        foreach (var (role, title, note) in Sections)
        {
            DrawSectionHeader(title, note);
            foreach (var plugin in ExternalPlugins.ByRole(role))
                DependencyRow.Draw(plugin);
        }

        ImGui.EndTable();
    }

    private static readonly (DependencyRole Role, string Title, string Note)[] Sections =
    [
        (DependencyRole.Movement, "MOVEMENT", "the one picked in Settings > General is required"),
        (DependencyRole.Combat,   "COMBAT",   "the one picked in Settings > General is required"),
        (DependencyRole.Skips,    "SKIPS",    "optional; Odysseus is used when present, else TextAdvance"),
    ];

    private static void DrawSectionHeader(string title, string note)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(1);
        ImGui.Dummy(new Vector2(0, 4f * ImGuiHelpers.GlobalScale));
        using (ImRaii.PushColor(ImGuiCol.Text, Styling.AccentVioletSoft))
            ImGui.TextUnformatted(title);
        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Text, Styling.TextDim))
            ImGui.TextUnformatted($"  {note}");
    }

    private static void DrawFooter()
    {
        using (ImRaii.PushColor(ImGuiCol.Text, Styling.TextMuted))
            ImGui.TextWrapped(
                "Install adds the plugin's source repository to Dalamud and queues an install. " +
                "If one-click install fails (URL drift, network), right-click a plugin name to " +
                "copy its repo URL and add it manually via /xlsettings -> Experimental -> Custom " +
                "Plugin Repositories.");
    }
}
