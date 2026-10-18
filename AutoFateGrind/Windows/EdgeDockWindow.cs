using AutoFateGrind.Windows.Dock;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System.Numerics;

namespace AutoFateGrind.Windows;

public enum ParcaeDockTab
{
    Parcae,
}

/// <summary>
/// Parcae's tab on the shared screen-edge dock (alongside Charon's and Daedalus's): a sideways-labelled tab
/// whose panel pops out beside it on hover and stays open when clicked. The panel is the live FATE tracker's
/// body plus run controls, so the dock and the popout window can't drift apart.
/// </summary>
public sealed unsafe class EdgeDockWindow : Window, IDisposable
{
    public sealed record Tab(ParcaeDockTab Id, string Label, Func<bool> Applies, Action DrawPanel);

    // Sized to match Charon's dock so the tabs line up when they share an edge.
    private const float TabWidth = 34f;
    private const float TabPadding = 14f;
    private const float TabGap = 4f;
    private const float PanelGap = 6f;
    private const float LabelScale = 1.25f;
    private const float PanelMinWidth = 320f;
    private static readonly TimeSpan CloseDelay = TimeSpan.FromMilliseconds(450);

    // Below Daedalus and Charon (order 20) in the shared group.
    private const string Owner = "Parcae";
    private const int Order = 30;

    private readonly IReadOnlyList<Tab> tabs;
    private readonly DockLayoutShare layout;
    private readonly DockHover<ParcaeDockTab> hover = new();
    private List<Tab> visible = [];

    public EdgeDockWindow(IReadOnlyList<Tab> tabs) : base("##ParcaeEdgeDock")
    {
        this.tabs = tabs;
        layout = new DockLayoutShare(Plugin.PluginInterface, Owner, Order);

        Flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoMove
                | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav
                | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar;
        RespectCloseHotkey = false;
        PositionCondition = ImGuiCond.Always;
        IsOpen = true;
    }

    public void Dispose() => layout.Dispose();

    // Not drawn (and so not published to the shared layout, where it goes stale and the others close up)
    // while the dock is off or no tab applies.
    public override bool DrawConditions()
    {
        if (!Plugin.Cfg.EdgeDockEnabled) return false;
        visible = tabs.Where(t => t.Applies()).ToList();
        return visible.Count > 0;
    }

    public override void PreDraw()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);

        var viewport = ImGui.GetMainViewport();
        var total = visible.Sum(t => TabHeight(t.Label)) + TabGap * Math.Max(0, visible.Count - 1);
        var right = Plugin.Cfg.EdgeDockRightSide;
        var x = right ? viewport.WorkPos.X + viewport.WorkSize.X - TabWidth : viewport.WorkPos.X;
        var y = layout.Place(right, total, viewport.WorkPos.Y, viewport.WorkSize.Y);
        Position = new Vector2(x, y);
    }

    public override void PostDraw() => ImGui.PopStyleVar();

    public override void Draw()
    {
        var drawList = ImGui.GetWindowDrawList();
        var right = Plugin.Cfg.EdgeDockRightSide;
        ParcaeDockTab? hovered = null;
        Tab? openTab = null;
        var openMin = Vector2.Zero;

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0f, TabGap));
        foreach (var tab in visible)
        {
            var size = new Vector2(TabWidth, TabHeight(tab.Label));
            var min = ImGui.GetCursorScreenPos();

            if (ImGui.InvisibleButton($"##parcaedock{tab.Id}", size))
                hover.Click(tab.Id);
            if (ImGui.IsItemHovered())
                hovered = tab.Id;

            var isOpen = hover.Open == tab.Id;
            DrawTab(drawList, min, size, tab.Label, isOpen || hovered == tab.Id, hover.Pinned == tab.Id, right);

            if (isOpen)
            {
                openTab = tab;
                openMin = min;
            }
        }
        ImGui.PopStyleVar();

        var panelBusy = openTab != null && DrawPanel(openTab, openMin, right);
        hover.Update(hovered, panelBusy, DateTime.UtcNow, CloseDelay, visible.Select(t => t.Id).ToList());
    }

    private static float TabHeight(string label) =>
        MathF.Max(90f, ImGui.CalcTextSize(label).X * LabelScale + TabPadding * 2f);

    private static void DrawTab(ImDrawListPtr drawList, Vector2 min, Vector2 size, string label, bool lit, bool pinned, bool right)
    {
        var max = min + size;
        var fill = lit ? new Vector4(0.16f, 0.17f, 0.20f, 0.96f) : new Vector4(0.09f, 0.10f, 0.12f, 0.88f);
        // Rounded on the side facing the screen, square against the edge it is pinned to.
        var corners = right ? ImDrawFlags.RoundCornersLeft : ImDrawFlags.RoundCornersRight;
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(fill), 6f, corners);

        if (pinned)
        {
            var barMin = right ? min : new Vector2(max.X - 2f, min.Y);
            drawList.AddRectFilled(barMin, barMin + new Vector2(2f, size.Y), ImGui.GetColorU32(Styling.AccentViolet));
        }

        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize() * LabelScale;
        var textSize = ImGui.CalcTextSize(label) * LabelScale;

        // Turned, the label is textSize.Y wide and textSize.X tall, centred in the tab.
        var topLeft = min + new Vector2((size.X - textSize.Y) / 2f, (size.Y - textSize.X) / 2f);
        var pivot = DockMath.PivotFor(topLeft, textSize);
        var color = ImGui.GetColorU32(lit ? Styling.TextStrong : Styling.TextSecondary);

        // Drawn first near the screen's top-left, where the whole unturned line fits, then turned into the tab
        // under a full-screen clip rect (ImGui culls glyphs against the clip rect BEFORE they are turned).
        var drawnAt = ImGui.GetMainViewport().WorkPos + new Vector2(8f, 8f);
        drawList.PushClipRectFullScreen();
        var first = drawList.VtxBuffer.Size;
        drawList.AddText(font, fontSize, drawnAt, color, label);
        var last = drawList.VtxBuffer.Size;
        var vertices = drawList.VtxBuffer.Data;
        for (var i = first; i < last; i++)
            vertices[i].Pos = DockMath.TurnUpAndPlace(vertices[i].Pos, drawnAt, pivot);
        drawList.PopClipRect();
    }

    /// <summary>Draws the open tab's panel beside it. True while the pointer is on it or it is mid-interaction.</summary>
    private static bool DrawPanel(Tab tab, Vector2 tabMin, bool right)
    {
        var viewport = ImGui.GetMainViewport();
        var x = right ? tabMin.X - PanelGap : tabMin.X + TabWidth + PanelGap;
        var y = MathF.Max(viewport.WorkPos.Y + 4f, tabMin.Y);

        ImGui.SetNextWindowPos(new Vector2(x, y), ImGuiCond.Always, new Vector2(right ? 1f : 0f, 0f));
        ImGui.SetNextWindowSizeConstraints(new Vector2(PanelMinWidth, 0f), new Vector2(520f, viewport.WorkSize.Y - 8f));
        ImGui.SetNextWindowBgAlpha(0.94f);
        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove
                                       | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing
                                       | ImGuiWindowFlags.AlwaysAutoResize;

        var busy = false;
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10f, 8f));
        if (ImGui.Begin($"##ParcaeEdgeDockPanel{tab.Id}", flags))
        {
            try
            {
                tab.DrawPanel();
            }
            catch (Exception ex)
            {
                // Caught here: an exception escaping between Begin and End would skip End and corrupt ImGui's
                // window stack for every plugin drawing after this one.
                ImGui.TextColored(Styling.AccentRose, $"This panel could not be drawn: {ex.Message}");
            }

            // Still "in use" while one of its pop-ups is open or a control is held, so it doesn't close under it.
            busy = ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows
                                         | ImGuiHoveredFlags.AllowWhenBlockedByPopup
                                         | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem)
                   || ImGui.IsPopupOpen(string.Empty, ImGuiPopupFlags.AnyPopupId | ImGuiPopupFlags.AnyPopupLevel)
                   || ImGui.IsAnyItemActive();
        }
        ImGui.End();
        ImGui.PopStyleVar();
        return busy;
    }
}
