using AutoFateGrind.Windows.Dock;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System.Numerics;

namespace AutoFateGrind.Windows;

/// <summary>
/// Parcae's tabs on the screen-edge dock shared with Daedalus and Charon, ported from Daedalus's
/// <c>EdgeDockWindow</c>. Each tab pops its panel out beside it while pointed at (click to pin). Every tab has its
/// own edge and its own spot along it; dragging a tab and letting go puts it on the nearest edge, where it was
/// let go, and <see cref="DockLayoutShare"/> keeps it from covering Daedalus's or Charon's tabs.
/// <para>
/// This window itself is an invisible 1×1 host. Each tab is its own small ImGui window, so tabs can sit anywhere
/// on any edge. A tab's panel is the body of the window it stands for, so the two cannot drift apart.
/// </para>
/// </summary>
public sealed unsafe class EdgeDockWindow : Window, IDisposable
{
    /// <param name="Id">Stable id; also the ImGui id suffix and the shared-layout key.</param>
    /// <param name="Label">What the tab reads.</param>
    /// <param name="Applies">Whether the tab is shown right now.</param>
    /// <param name="DrawPanel">The panel body — normally the target window's own body.</param>
    /// <param name="PanelWidth">
    /// A fixed width with the height left to the content, for bodies that size items off the space left in the
    /// window (in a window that sizes itself to its content, those chase each other). Null auto-sizes both ways.
    /// </param>
    public sealed record Tab(string Id, string Label, Func<bool> Applies, Action DrawPanel, float? PanelWidth = null);

    // Owner and order in the shared layout: after Daedalus (10) and Charon (20) when tabs tie on a spot.
    private const string Owner = "Parcae";
    private const int Order = 30;

    private const float TabThickness = 34f;
    private const float TabPadding = 14f;
    private const float PanelGap = 6f;
    private const float LabelScale = 1.25f;
    private const float DragThreshold = 6f;
    private static readonly TimeSpan CloseDelay = TimeSpan.FromMilliseconds(450);

    private const ImGuiWindowFlags TabFlags =
        ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoMove
        | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav
        | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

    private readonly IReadOnlyList<Tab> tabs;
    private readonly Func<string, (DockEdge Edge, float Along)> placement;
    private readonly Action<string, DockEdge, float> moveTab;
    private readonly DockLayoutShare layout;
    private readonly DockHover hover = new();
    private List<Tab> visible = [];
    private Vector2 panelSize = new(320f, 240f);
    private string? dragging;

    /// <param name="placement">Where a tab is set to sit: its edge, and its centre as a fraction along it.</param>
    /// <param name="moveTab">A tab was dragged and let go: store its new edge and spot.</param>
    public EdgeDockWindow(
        IReadOnlyList<Tab> tabs, Func<string, (DockEdge Edge, float Along)> placement, Action<string, DockEdge, float> moveTab)
        : base("##ParcaeEdgeDock")
    {
        this.tabs = tabs;
        this.placement = placement;
        this.moveTab = moveTab;
        layout = new DockLayoutShare(Plugin.PluginInterface, Owner, Order);

        Flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs
                | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing
                | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoScrollbar;
        RespectCloseHotkey = false;
        PositionCondition = ImGuiCond.Always;
        SizeCondition = ImGuiCond.Always;
        Size = Vector2.One;
        IsOpen = true;
    }

    public void Dispose() => layout.Dispose();

    // Not drawn (so its tabs stop publishing, go stale in the shared layout and the others close up) while the
    // dock is off or no tab applies.
    public override bool DrawConditions()
    {
        if (!Plugin.Cfg.EdgeDockEnabled)
        {
            dragging = null;
            return false;
        }
        visible = tabs.Where(t => t.Applies()).ToList();
        return visible.Count > 0;
    }

    public override void PreDraw() => Position = ImGui.GetMainViewport().WorkPos;

    public override void Draw()
    {
        var viewport = ImGui.GetMainViewport();
        var workPos = viewport.WorkPos;
        var workSize = viewport.WorkSize;

        var requests = visible.Select(t =>
        {
            var (edge, along) = placement(t.Id);
            return new DockRequest(t.Id, edge, along, TabLength(t.Label));
        }).ToList();
        var starts = layout.Place(requests, workPos, workSize);

        string? hovered = null;
        Tab? openTab = null;
        var openMin = Vector2.Zero;
        var openSize = Vector2.Zero;
        var openEdge = DockEdge.Left;

        for (var i = 0; i < visible.Count; i++)
        {
            var tab = visible[i];
            var edge = requests[i].Edge;
            if (!starts.TryGetValue(tab.Id, out var start))
                continue;

            var (min, size) = DockMath.TabRect(edge, start, requests[i].Length, TabThickness, workPos, workSize);
            if (DrawTabWindow(tab, edge, min, size, ref hovered))
            {
                openTab = tab;
                openMin = min;
                openSize = size;
                openEdge = edge;
            }
        }

        if (dragging != null)
            DrawDrag(workPos, workSize);

        var panelBusy = openTab != null && DrawPanel(openTab, openEdge, openMin, openSize);
        hover.Update(hovered, panelBusy, DateTime.UtcNow, CloseDelay, visible.Select(t => t.Id).ToList());
    }

    /// <summary>One tab, in its own little window. True when its panel is out.</summary>
    private bool DrawTabWindow(Tab tab, DockEdge edge, Vector2 min, Vector2 size, ref string? hovered)
    {
        ImGui.SetNextWindowPos(min, ImGuiCond.Always);
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, Vector2.One);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);

        var isOpen = false;
        if (ImGui.Begin($"##ParcaeEdgeDockTab{tab.Id}", TabFlags))
        {
            // A press that moves becomes a drag; only a press that does not is a click (pin / unpin).
            var clicked = ImGui.InvisibleButton($"##dock{tab.Id}", size);
            if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left, DragThreshold))
                dragging = tab.Id;
            if (clicked && dragging != tab.Id)
                hover.Click(tab.Id);
            if (ImGui.IsItemHovered() && dragging == null)
            {
                hovered = tab.Id;
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            isOpen = hover.Open == tab.Id && dragging == null;
            var lit = isOpen || hovered == tab.Id || dragging == tab.Id;
            DrawTab(ImGui.GetWindowDrawList(), min, size, tab.Label, lit, hover.Pinned == tab.Id, edge);
        }

        ImGui.End();
        ImGui.PopStyleVar(3);
        return isOpen;
    }

    /// <summary>A tab being dragged: the edge it would land on lit, an outline where it would sit, and on release the move.</summary>
    private void DrawDrag(Vector2 workPos, Vector2 workSize)
    {
        var mouse = ImGui.GetMousePos();
        var target = DockMath.NearestEdge(mouse, workPos, workSize);
        var along = DockMath.AlongFraction(target, mouse, workPos, workSize);

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var id = dragging!;
            dragging = null;
            moveTab(id, target, along);
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
        var fg = ImGui.GetForegroundDrawList();
        var accent = ImGui.GetColorU32(Styling.AccentViolet);
        var a = workPos;
        var b = workPos + workSize;
        const float bar = 3f;
        switch (target)
        {
            case DockEdge.Left: fg.AddRectFilled(a, new Vector2(a.X + bar, b.Y), accent); break;
            case DockEdge.Right: fg.AddRectFilled(new Vector2(b.X - bar, a.Y), b, accent); break;
            case DockEdge.Top: fg.AddRectFilled(a, new Vector2(b.X, a.Y + bar), accent); break;
            case DockEdge.Bottom: fg.AddRectFilled(new Vector2(a.X, b.Y - bar), b, accent); break;
        }

        // The outline of where it would land (before any nudge from its neighbours).
        var label = visible.FirstOrDefault(t => t.Id == dragging)?.Label ?? dragging!;
        var length = TabLength(label);
        var horizontal = DockMath.IsHorizontal(target);
        var edgeStart = horizontal ? workPos.X : workPos.Y;
        var edgeLength = horizontal ? workSize.X : workSize.Y;
        var start = Math.Clamp(edgeStart + along * edgeLength - length / 2f, edgeStart,
            MathF.Max(edgeStart, edgeStart + edgeLength - length));
        var (min, size) = DockMath.TabRect(target, start, length, TabThickness, workPos, workSize);
        fg.AddRect(min, min + size, accent, 6f, ImDrawFlags.None, 2f);
    }

    private static float TabLength(string label) =>
        MathF.Max(90f, ImGui.CalcTextSize(label).X * LabelScale + TabPadding * 2f);

    private static void DrawTab(ImDrawListPtr drawList, Vector2 min, Vector2 size, string label, bool lit, bool pinned, DockEdge edge)
    {
        var max = min + size;
        var fill = lit ? new Vector4(0.16f, 0.15f, 0.20f, 0.96f) : new Vector4(0.09f, 0.10f, 0.12f, 0.88f);
        // Rounded on the side facing the screen, square against the edge it is pinned to.
        var corners = edge switch
        {
            DockEdge.Right => ImDrawFlags.RoundCornersLeft,
            DockEdge.Top => ImDrawFlags.RoundCornersBottom,
            DockEdge.Bottom => ImDrawFlags.RoundCornersTop,
            _ => ImDrawFlags.RoundCornersRight,
        };
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(fill), 6f, corners);

        if (pinned)
        {
            // Parcae violet on the tab's screen-facing side marks a pinned panel.
            var (barMin, barSize) = edge switch
            {
                DockEdge.Right => (min, new Vector2(2f, size.Y)),
                DockEdge.Top => (new Vector2(min.X, max.Y - 2f), new Vector2(size.X, 2f)),
                DockEdge.Bottom => (min, new Vector2(size.X, 2f)),
                _ => (new Vector2(max.X - 2f, min.Y), new Vector2(2f, size.Y)),
            };
            drawList.AddRectFilled(barMin, barMin + barSize, ImGui.GetColorU32(Styling.AccentViolet));
        }

        var font = ImGui.GetFont();
        var fontSize = ImGui.GetFontSize() * LabelScale;
        var textSize = ImGui.CalcTextSize(label) * LabelScale;
        var color = ImGui.GetColorU32(lit ? Styling.AccentVioletSoft : Styling.TextSecondary);

        if (DockMath.IsHorizontal(edge))
        {
            drawList.AddText(font, fontSize, min + (size - textSize) / 2f, color, label);
            return;
        }

        // Turned, the label is textSize.Y wide and textSize.X tall — centred in the tab. Drawn first near the
        // screen's top-left, where the whole unturned line fits, then turned and moved in under a full-screen
        // clip rect (ImGui culls glyphs against the clip rect BEFORE they are turned).
        var topLeft = min + new Vector2((size.X - textSize.Y) / 2f, (size.Y - textSize.X) / 2f);
        var pivot = DockMath.PivotFor(topLeft, textSize);
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
    private bool DrawPanel(Tab tab, DockEdge edge, Vector2 tabMin, Vector2 tabSize)
    {
        var viewport = ImGui.GetMainViewport();
        var (pos, pivot) = DockMath.PanelPlacement(
            edge, tabMin, tabSize, panelSize, viewport.WorkPos, viewport.WorkSize, PanelGap);

        ImGui.SetNextWindowPos(pos, ImGuiCond.Always, pivot);
        if (tab.PanelWidth is { } width)
            ImGui.SetNextWindowSizeConstraints(new Vector2(width, 0f), new Vector2(width, float.MaxValue));
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

            panelSize = ImGui.GetWindowSize();

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
