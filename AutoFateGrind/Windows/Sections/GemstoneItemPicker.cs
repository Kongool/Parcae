using AutoFateGrind.Core.Trading;
using AutoFateGrind.Windows.Sections.Config;
using Dalamud.Bindings.ImGui;

namespace AutoFateGrind.Windows.Sections;

// One catalog-backed picker shared by the main planner and the full Gemstones settings page.
// Only items sold by a registered, routable Bicolor trader are offered.
internal static class GemstoneItemPicker
{
    private static GemstoneTradeItem[]? sortedItems;
    private static string[]? sortedLabels;

    public static GemstoneTradeItem? Selected(Configuration cfg)
    {
        EnsureCatalog();
        if (sortedItems is null || sortedItems.Length == 0) return null;

        var effectiveId = GemstoneCatalog.EnsurePersistedTarget();
        return Array.Find(sortedItems, i => i.ItemId == effectiveId) ?? sortedItems[0];
    }

    public static bool Draw(Configuration cfg, string id, float width)
    {
        EnsureCatalog();
        if (sortedItems is null || sortedLabels is null || sortedItems.Length == 0)
        {
            using var color = Dalamud.Interface.Utility.Raii.ImRaii.PushColor(ImGuiCol.Text, Styling.AccentRose);
            ImGui.TextUnformatted("No routable Bicolor shop items found.");
            return false;
        }

        var selected = Selected(cfg)!;
        var selectedIndex = Array.FindIndex(sortedItems, i => i.ItemId == selected.ItemId);
        if (!SettingsControls.DrawSearchableCombo(id, sortedLabels[selectedIndex], sortedLabels, ref selectedIndex, width))
            return false;

        cfg.TargetTradeItemId = sortedItems[selectedIndex].ItemId;
        cfg.SaveDebounced();
        return true;
    }

    private static void EnsureCatalog()
    {
        var catalog = GemstoneCatalog.Routable;
        if (sortedItems is not null && sortedItems.Length == catalog.Length) return;

        sortedItems = [.. catalog.OrderBy(i => i.ItemName, StringComparer.OrdinalIgnoreCase)];
        sortedLabels = sortedItems.Select(i => $"{i.ItemName}  ({i.CostPerOne} gems)").ToArray();
    }
}
