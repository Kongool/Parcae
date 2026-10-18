using System.Collections.Concurrent;
using System.Numerics;
using Dalamud.Plugin;

namespace AutoFateGrind.Windows.Dock;

// The edge dock shared with Charon and Daedalus: tabs pinned to a screen edge with sideways labels, a panel
// popping out beside the one you point at. Each plugin draws its own dock, but they publish their stack into
// one Dalamud data share so docks on the same edge stack as a single centred group instead of drawing on top
// of each other. The share tag and slot layout below must match Charon's
// (Charon/Features/Dock/DockLayout.cs) and Daedalus's, or Parcae's dock silently falls back to centring alone.

/// <summary>One plugin's dock as the shared layout sees it. Edge: 0 = left, 1 = right.</summary>
public readonly record struct DockSlot(string Owner, int Edge, float Height, int Order, long SeenMs);

public static class DockLayout
{
    /// <summary>A dock that has not published for this long is gone, and the rest close up.</summary>
    public const long StaleMs = 1000;

    /// <summary>Space between two plugins' docks — wider than the gap between one dock's own tabs.</summary>
    public const float GroupGap = 10f;

    /// <summary>The top of <paramref name="owner"/>'s dock; <paramref name="slots"/> must include its own slot.</summary>
    public static float TopFor(
        string owner, int edge, float ownHeight, IEnumerable<DockSlot> slots, long nowMs,
        float workTop, float workHeight)
    {
        var live = slots
            .Where(s => s.Edge == edge && s.Height > 0f && nowMs - s.SeenMs <= StaleMs)
            .OrderBy(s => s.Order)
            .ThenBy(s => s.Owner, StringComparer.Ordinal)
            .ToList();

        var total = live.Sum(s => s.Height) + GroupGap * Math.Max(0, live.Count - 1);
        var top = workTop + MathF.Max(0f, (workHeight - total) / 2f);
        foreach (var slot in live)
        {
            if (slot.Owner == owner)
                return top;
            top += slot.Height + GroupGap;
        }

        return workTop + MathF.Max(0f, (workHeight - ownHeight) / 2f);
    }
}

/// <summary>
/// The shared list of docks in Dalamud's data share. Only framework types cross the plugin boundary — a
/// <c>ConcurrentDictionary&lt;string, long[]&gt;</c> keyed by owner, each value <c>[edge, height, order, seenMs]</c>
/// — because every plugin gets the SAME object and a type from one plugin's assembly would not match another's.
/// </summary>
public sealed class DockLayoutShare : IDisposable
{
    /// <summary>The data-share tag Charon and Daedalus use. Change all three or none.</summary>
    public const string Tag = "EdgeDock.Layout.v1";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly string owner;
    private readonly int order;
    private readonly ConcurrentDictionary<string, long[]>? slots;

    public DockLayoutShare(IDalamudPluginInterface pluginInterface, string owner, int order)
    {
        this.pluginInterface = pluginInterface;
        this.owner = owner;
        this.order = order;
        try
        {
            slots = pluginInterface.GetOrCreateData(Tag, () => new ConcurrentDictionary<string, long[]>());
        }
        catch
        {
            // A type clash with another plugin's version of the share: centre alone rather than fail.
            slots = null;
        }
    }

    /// <summary>Publish this frame's dock and get back where its top goes.</summary>
    public float Place(bool rightEdge, float height, float workTop, float workHeight)
    {
        var edge = rightEdge ? 1 : 0;
        if (slots is null)
            return workTop + MathF.Max(0f, (workHeight - height) / 2f);

        var now = Environment.TickCount64;
        slots[owner] = [edge, (long)MathF.Ceiling(height), order, now];
        return DockLayout.TopFor(owner, edge, height, Read(slots), now, workTop, workHeight);
    }

    private static IEnumerable<DockSlot> Read(ConcurrentDictionary<string, long[]> slots)
    {
        foreach (var (owner, v) in slots)
        {
            if (v is { Length: >= 4 })
                yield return new DockSlot(owner, (int)v[0], v[1], (int)v[2], v[3]);
        }
    }

    public void Dispose()
    {
        if (slots is null)
            return;

        slots.TryRemove(owner, out _);
        pluginInterface.RelinquishData(Tag);
    }
}

/// <summary>
/// Which tab's panel is out. Pointing at a tab opens it; it stays while the pointer is on the tab or the panel
/// and closes a moment after it leaves (the delay stops it flickering shut crossing the gap). Clicking a tab
/// pins its panel open; click again to unpin.
/// </summary>
public sealed class DockHover<TTab> where TTab : struct, Enum
{
    private DateTime lastTouchedUtc;

    public TTab? Open { get; private set; }
    public TTab? Pinned { get; private set; }

    public void Update(TTab? hoveredTab, bool panelBusy, DateTime nowUtc, TimeSpan closeDelay, IReadOnlyCollection<TTab> visible)
    {
        // A tab whose moment has passed takes its panel (and its pin) with it.
        if (Pinned is { } pinned && !visible.Contains(pinned)) Pinned = null;
        if (Open is { } open && !visible.Contains(open)) Open = null;

        if (hoveredTab is { } hovered && visible.Contains(hovered))
        {
            Open = hovered;
            lastTouchedUtc = nowUtc;
            return;
        }

        if (panelBusy)
        {
            lastTouchedUtc = nowUtc;
            return;
        }

        if (Open != null && nowUtc - lastTouchedUtc > closeDelay)
            Open = Pinned;
    }

    public void Click(TTab tab)
    {
        Pinned = EqualityComparer<TTab?>.Default.Equals(Pinned, tab) ? null : tab;
        if (Pinned != null) Open = Pinned;
    }
}

/// <summary>
/// Sideways text. ImGui cannot draw rotated text, so the label is drawn normally and its vertices are turned a
/// quarter-turn anticlockwise afterwards, reading bottom-to-top.
/// </summary>
public static class DockMath
{
    /// <summary>Where the unrotated text's origin must land so that, turned, its box has this top-left.</summary>
    public static Vector2 PivotFor(Vector2 topLeft, Vector2 textSize) => new(topLeft.X, topLeft.Y + textSize.X);

    /// <summary>
    /// A vertex of text DRAWN at <paramref name="drawnAt"/>, turned and moved so its origin lands on
    /// <paramref name="pivot"/>. The text must be drawn where the whole unturned line is on screen: ImGui culls
    /// off-screen glyphs before they are turned, so text drawn at a right-edge pivot loses its tail.
    /// </summary>
    public static Vector2 TurnUpAndPlace(Vector2 point, Vector2 drawnAt, Vector2 pivot)
    {
        var d = point - drawnAt;
        return pivot + new Vector2(d.Y, -d.X);
    }
}
