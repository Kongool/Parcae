using System.Collections.Concurrent;
using System.Numerics;
using Dalamud.Plugin;

namespace AutoFateGrind.Windows.Dock;

// The edge dock shared with Daedalus and Charon: tabs pinned to the screen edges with their panels popping
// out beside them. Ported from Daedalus (Daedalus/Windows/Dock/DockLayout.cs + EdgeDock.cs), which carries the
// same code as Charon. Every plugin draws its own tabs but publishes each one into a single Dalamud data share,
// so tabs from all three keep clear of each other. The share tag, the slot layout and the edge numbering below
// must match theirs, or Parcae's tabs are laid out on their own and can sit on top of theirs.

/// <summary>The screen edge a tab is pinned to. The values are the shared layout's edge numbers.</summary>
public enum DockEdge
{
    Left = 0,
    Right = 1,
    Top = 2,
    Bottom = 3,
}

/// <summary>One tab as the shared layout sees it.</summary>
/// <param name="Key">"Owner/TabId" — the owner is the plugin ("Parcae", "Daedalus", "Charon").</param>
/// <param name="Edge">The <see cref="DockEdge"/> value.</param>
/// <param name="Along">Where the tab wants its centre, as a fraction of the edge (0 = top / left end, 1 = the far end).</param>
/// <param name="Length">How long the tab is along the edge this frame, in pixels.</param>
/// <param name="Order">Settles ties between tabs wanting the same spot, smallest first: the plugin's order × 100 + the tab's index.</param>
/// <param name="SeenMs">When it last published, on <see cref="Environment.TickCount64"/>.</param>
public readonly record struct DockSlot(string Key, int Edge, float Along, float Length, int Order, long SeenMs)
{
    public string Owner => Key.IndexOf('/') is var i and >= 0 ? Key[..i] : Key;
}

/// <summary>
/// Where every edge tab goes, across every plugin that docks tabs. Pure.
/// <para>
/// Each tab sits where it was put (<see cref="DockSlot.Along"/>; a tab never moved wants the middle). Tabs that
/// would overlap form a group laid end to end, centred on the average of where its tabs want to be — so several
/// tabs all wanting the middle stack centred, and one tab dragged near another nudges both apart rather than
/// covering it. Groups are kept on screen. A tab that stops publishing drops out after <see cref="StaleMs"/>.
/// </para>
/// </summary>
public static class DockLayout
{
    /// <summary>A tab that has not published for this long is gone.</summary>
    public const long StaleMs = 1000;

    /// <summary>Space between two tabs of one plugin.</summary>
    public const float TabGap = 4f;

    /// <summary>Space between two plugins' tabs — wider, so you can see where one plugin's run ends.</summary>
    public const float GroupGap = 10f;

    public static float GapBetween(DockSlot a, DockSlot b) => a.Owner == b.Owner ? TabGap : GroupGap;

    /// <summary>Where each live tab on <paramref name="edge"/> starts along it, keyed by <see cref="DockSlot.Key"/>.</summary>
    public static Dictionary<string, float> Resolve(
        int edge, IEnumerable<DockSlot> slots, long nowMs, float workStart, float workLength)
    {
        var live = slots
            .Where(s => s.Edge == edge && s.Length > 0f && nowMs - s.SeenMs <= StaleMs)
            .OrderBy(s => s.Along)
            .ThenBy(s => s.Order)
            .ThenBy(s => s.Key, StringComparer.Ordinal)
            .ToList();

        var groups = new List<List<DockSlot>>();
        var starts = new List<float>();
        foreach (var slot in live)
        {
            groups.Add([slot]);
            starts.Add(StartOf(groups[^1], workStart, workLength));

            // Overlapping the group before it (or too close): join it, and settle the joined group afresh.
            while (groups.Count >= 2)
            {
                var a = groups.Count - 2;
                var b = a + 1;
                if (starts[a] + LengthOf(groups[a]) + GapBetween(groups[a][^1], groups[b][0]) <= starts[b])
                    break;

                groups[a].AddRange(groups[b]);
                groups.RemoveAt(b);
                starts.RemoveAt(b);
                starts[a] = StartOf(groups[a], workStart, workLength);
            }
        }

        var result = new Dictionary<string, float>();
        for (var g = 0; g < groups.Count; g++)
        {
            var at = starts[g];
            var group = groups[g];
            for (var i = 0; i < group.Count; i++)
            {
                result[group[i].Key] = at;
                if (i + 1 < group.Count)
                    at += group[i].Length + GapBetween(group[i], group[i + 1]);
            }
        }

        return result;
    }

    private static float LengthOf(List<DockSlot> group)
    {
        var length = 0f;
        for (var i = 0; i < group.Count; i++)
            length += group[i].Length + (i + 1 < group.Count ? GapBetween(group[i], group[i + 1]) : 0f);
        return length;
    }

    /// <summary>The group is centred on the average of where its tabs want to be, kept on screen.</summary>
    private static float StartOf(List<DockSlot> group, float workStart, float workLength)
    {
        var wanted = 0f;
        foreach (var slot in group)
            wanted += workStart + Math.Clamp(slot.Along, 0f, 1f) * workLength;
        wanted /= group.Count;

        var length = LengthOf(group);
        var latest = MathF.Max(workStart, workStart + workLength - length);
        return Math.Clamp(wanted - length / 2f, workStart, latest);
    }
}

/// <summary>One of this plugin's tabs, asking for a place.</summary>
public readonly record struct DockRequest(string Id, DockEdge Edge, float Along, float Length);

/// <summary>
/// The shared list of tabs in Dalamud's data share. Only framework types cross the plugin boundary — a
/// <c>ConcurrentDictionary&lt;string, long[]&gt;</c> keyed by "Owner/TabId", each value
/// <c>[edge, along × 10000, length, order, seenMs]</c> — because every plugin gets the SAME object and a type from
/// one plugin's assembly would not match another's. If the share cannot be had, the tabs are laid out alone.
/// </summary>
public sealed class DockLayoutShare : IDisposable
{
    /// <summary>The data-share tag Daedalus and Charon use. Change all three or none.</summary>
    public const string Tag = "EdgeDock.Layout.v2";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly string owner;
    private readonly int order;
    private readonly bool shared;
    private readonly ConcurrentDictionary<string, long[]> slots;

    public DockLayoutShare(IDalamudPluginInterface pluginInterface, string owner, int order)
    {
        this.pluginInterface = pluginInterface;
        this.owner = owner;
        this.order = order;
        ConcurrentDictionary<string, long[]>? found = null;
        try
        {
            found = pluginInterface.GetOrCreateData(Tag, () => new ConcurrentDictionary<string, long[]>());
        }
        catch
        {
            // A type clash with another plugin's version of the share: laid out alone, below.
        }

        shared = found != null;
        slots = found ?? new ConcurrentDictionary<string, long[]>();
    }

    /// <summary>
    /// Publish this frame's tabs and get back where each one starts along its edge, keyed by tab id: its top on
    /// the left and right edges, its left on the top and bottom ones.
    /// </summary>
    public Dictionary<string, float> Place(IReadOnlyList<DockRequest> tabs, Vector2 workPos, Vector2 workSize)
    {
        var now = Environment.TickCount64;
        var prefix = owner + "/";
        var mine = new HashSet<string>();
        for (var i = 0; i < tabs.Count; i++)
        {
            var t = tabs[i];
            var key = prefix + t.Id;
            mine.Add(key);
            slots[key] = [(int)t.Edge, (long)MathF.Round(Math.Clamp(t.Along, 0f, 1f) * 10000f),
                          (long)MathF.Ceiling(t.Length), order * 100 + i, now];
        }

        // A tab of ours that is gone this frame leaves at once, rather than holding its place for a second.
        foreach (var key in slots.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal) && !mine.Contains(key))
                slots.TryRemove(key, out _);
        }

        var all = Read(slots).ToList();
        var result = new Dictionary<string, float>();
        foreach (var edge in tabs.Select(t => t.Edge).Distinct())
        {
            var horizontal = DockMath.IsHorizontal(edge);
            var starts = DockLayout.Resolve((int)edge, all, now,
                horizontal ? workPos.X : workPos.Y, horizontal ? workSize.X : workSize.Y);
            foreach (var t in tabs.Where(t => t.Edge == edge))
            {
                if (starts.TryGetValue(prefix + t.Id, out var start))
                    result[t.Id] = start;
            }
        }

        return result;
    }

    private static IEnumerable<DockSlot> Read(ConcurrentDictionary<string, long[]> slots)
    {
        foreach (var (key, v) in slots)
        {
            if (v is { Length: >= 5 })
                yield return new DockSlot(key, (int)v[0], v[1] / 10000f, v[2], (int)v[3], v[4]);
        }
    }

    public void Dispose()
    {
        var prefix = owner + "/";
        foreach (var key in slots.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
                slots.TryRemove(key, out _);
        }

        if (shared)
            pluginInterface.RelinquishData(Tag);
    }
}

/// <summary>
/// Which tab's panel is out. Pointing at a tab opens it; it stays while the pointer is on the tab or the panel,
/// and closes a moment after it leaves (the delay stops it flickering shut crossing the gap). Clicking a tab pins
/// its panel open; click again to unpin.
/// </summary>
public sealed class DockHover
{
    private DateTime lastTouchedUtc;

    public string? Open { get; private set; }
    public string? Pinned { get; private set; }

    public void Update(string? hoveredTab, bool panelBusy, DateTime nowUtc, TimeSpan closeDelay, IReadOnlyList<string> visible)
    {
        // A tab that is no longer shown takes its panel (and its pin) with it.
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

    public void Click(string tab)
    {
        Pinned = Pinned == tab ? null : tab;
        if (Pinned != null) Open = Pinned;
    }
}

/// <summary>Edge-dock geometry: sideways text, edge picking, tab and panel placement.</summary>
public static class DockMath
{
    /// <summary>Where the unrotated text's origin must land so that, turned, its box has this top-left.</summary>
    public static Vector2 PivotFor(Vector2 topLeft, Vector2 textSize) => new(topLeft.X, topLeft.Y + textSize.X);

    /// <summary>
    /// A vertex of text DRAWN at <paramref name="drawnAt"/>, turned a quarter-turn anticlockwise and moved so its
    /// origin lands on <paramref name="pivot"/>. ImGui drops off-screen glyphs before they are turned, so the
    /// text must be drawn where the whole line is on screen, then placed with this.
    /// </summary>
    public static Vector2 TurnUpAndPlace(Vector2 point, Vector2 drawnAt, Vector2 pivot)
    {
        var d = point - drawnAt;
        return pivot + new Vector2(d.Y, -d.X);
    }

    /// <summary>Top and bottom tabs run along the edge; left and right ones stand up the edge.</summary>
    public static bool IsHorizontal(DockEdge edge) => edge is DockEdge.Top or DockEdge.Bottom;

    /// <summary>The screen edge closest to <paramref name="point"/> — where a dragged tab lands when let go.</summary>
    public static DockEdge NearestEdge(Vector2 point, Vector2 workPos, Vector2 workSize)
    {
        var best = DockEdge.Left;
        var bestDistance = point.X - workPos.X;

        void Consider(DockEdge edge, float distance)
        {
            if (distance < bestDistance)
            {
                best = edge;
                bestDistance = distance;
            }
        }

        Consider(DockEdge.Right, workPos.X + workSize.X - point.X);
        Consider(DockEdge.Top, point.Y - workPos.Y);
        Consider(DockEdge.Bottom, workPos.Y + workSize.Y - point.Y);
        return best;
    }

    /// <summary>How far along <paramref name="edge"/> a point is, as a fraction of the edge (0..1).</summary>
    public static float AlongFraction(DockEdge edge, Vector2 point, Vector2 workPos, Vector2 workSize)
    {
        var fraction = IsHorizontal(edge)
            ? (point.X - workPos.X) / MathF.Max(1f, workSize.X)
            : (point.Y - workPos.Y) / MathF.Max(1f, workSize.Y);
        return Math.Clamp(fraction, 0f, 1f);
    }

    /// <summary>A tab's rectangle: <paramref name="start"/> along the edge, <paramref name="length"/> long, <paramref name="thickness"/> out from it.</summary>
    public static (Vector2 Min, Vector2 Size) TabRect(
        DockEdge edge, float start, float length, float thickness, Vector2 workPos, Vector2 workSize) => edge switch
    {
        DockEdge.Right => (new Vector2(workPos.X + workSize.X - thickness, start), new Vector2(thickness, length)),
        DockEdge.Top => (new Vector2(start, workPos.Y), new Vector2(length, thickness)),
        DockEdge.Bottom => (new Vector2(start, workPos.Y + workSize.Y - thickness), new Vector2(length, thickness)),
        _ => (new Vector2(workPos.X, start), new Vector2(thickness, length)),
    };

    /// <summary>
    /// Where an open tab's panel goes: beside the tab on the screen side, level with it — slid back along the
    /// edge if <paramref name="panelSize"/> (last frame's) would run it off the screen.
    /// </summary>
    /// <returns>The window position and the pivot that position refers to (as for SetNextWindowPos).</returns>
    public static (Vector2 Pos, Vector2 Pivot) PanelPlacement(
        DockEdge edge, Vector2 tabMin, Vector2 tabSize, Vector2 panelSize, Vector2 workPos, Vector2 workSize, float gap)
    {
        const float margin = 4f;
        var y = MathF.Max(workPos.Y + margin, MathF.Min(tabMin.Y, workPos.Y + workSize.Y - panelSize.Y - margin));
        var x = MathF.Max(workPos.X + margin, MathF.Min(tabMin.X, workPos.X + workSize.X - panelSize.X - margin));

        return edge switch
        {
            DockEdge.Right => (new Vector2(tabMin.X - gap, y), new Vector2(1f, 0f)),
            DockEdge.Top => (new Vector2(x, tabMin.Y + tabSize.Y + gap), Vector2.Zero),
            DockEdge.Bottom => (new Vector2(x, tabMin.Y - gap), new Vector2(0f, 1f)),
            _ => (new Vector2(tabMin.X + tabSize.X + gap, y), Vector2.Zero),
        };
    }
}
