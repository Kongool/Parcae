using AutoFateGrind.Core;
using AutoFateGrind.Core.Trading;
using AutoFateGrind.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;

namespace AutoFateGrind.Windows.Sections;

internal static class GemstonePurchaseSummary
{
    private static readonly GemstoneSpendMode[] spendModes =
        [GemstoneSpendMode.SpendAll, GemstoneSpendMode.SpendGems, GemstoneSpendMode.BuyQuantity];

    private static readonly string[] spendLabels =
        ["Spend all above reserve", "Spend up to a gem amount", "Buy a fixed quantity"];

    public static string HeaderSummary(Configuration cfg)
    {
        if (!cfg.TradeOnCap) return "off";
        var item = GemstoneItemPicker.Selected(cfg);
        return item is null ? "no items available" : item.ItemName;
    }

    public static void Draw(Configuration cfg)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var height = (cfg.TradeOnCap ? 154f : 45f) * scale;
        using var card = Card.Begin("##gemstone_purchase", new Vector2(-1, height),
            Styling.CardBgSoft, Styling.BorderDim);

        DrawHeader(cfg);
        if (!cfg.TradeOnCap) return;

        ImGui.Spacing();
        DrawTriggerAndItem(cfg);
        ImGui.Spacing();
        DrawSpendRow(cfg);
        ImGui.Spacing();
        DrawPreview(cfg);
    }

    private static void DrawHeader(Configuration cfg)
    {
        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (ImRaii.PushColor(ImGuiCol.Text, Styling.AccentAmber))
            ImGui.TextUnformatted(FontAwesomeIcon.ShoppingBag.ToIconString());
        ImGui.SameLine(0, 8f * ImGuiHelpers.GlobalScale);
        using (ImRaii.PushColor(ImGuiCol.Text, Styling.TextStrong))
            ImGui.TextUnformatted("Buy with Bicolor Gemstones");

        var toggle = cfg.TradeOnCap;
        var toggleWidth = 38f * ImGuiHelpers.GlobalScale;
        ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - toggleWidth);
        if (ToggleSwitch.Draw("##planner_trade", ref toggle))
        {
            cfg.TradeOnCap = toggle;
            if (toggle) GemstoneCatalog.EnsurePersistedTarget();
            cfg.SaveDebounced();
        }

        if (!cfg.TradeOnCap)
        {
            using (ImRaii.PushColor(ImGuiCol.Text, Styling.TextDim))
                ImGui.TextUnformatted("Optional - automatically visit a vendor and spend gems at your threshold.");
        }
    }

    private static void DrawTriggerAndItem(Configuration cfg)
    {
        Caption("At");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(92f * ImGuiHelpers.GlobalScale);
        var threshold = cfg.TradeThreshold;
        if (ImGui.InputInt("##planner_trade_threshold", ref threshold, 50, 250))
        {
            cfg.TradeThreshold = Math.Clamp(threshold, 100, AfgConstants.BicolorCap);
            cfg.SaveDebounced();
        }
        ImGui.SameLine();
        Caption("gems, buy");
        ImGui.SameLine();
        GemstoneItemPicker.Draw(cfg, "##planner_trade_item", 390f);
    }

    private static void DrawSpendRow(Configuration cfg)
    {
        Caption("Amount");
        ImGui.SameLine();
        var selected = Math.Max(0, Array.IndexOf(spendModes, cfg.SpendMode));
        ImGui.SetNextItemWidth(184f * ImGuiHelpers.GlobalScale);
        if (ImGui.Combo("##planner_spend_mode", ref selected, spendLabels, spendLabels.Length))
        {
            cfg.SpendMode = spendModes[selected];
            cfg.SaveDebounced();
        }

        if (cfg.SpendMode is GemstoneSpendMode.SpendGems or GemstoneSpendMode.BuyQuantity)
        {
            ImGui.SameLine();
            Caption(cfg.SpendMode == GemstoneSpendMode.SpendGems ? "Limit" : "Quantity");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(82f * ImGuiHelpers.GlobalScale);
            var amount = cfg.SpendMode == GemstoneSpendMode.SpendGems ? cfg.SpendGemsAmount : cfg.BuyQuantityAmount;
            if (ImGui.InputInt("##planner_spend_amount", ref amount, cfg.SpendMode == GemstoneSpendMode.SpendGems ? 50 : 1))
            {
                if (cfg.SpendMode == GemstoneSpendMode.SpendGems)
                    cfg.SpendGemsAmount = Math.Clamp(amount, 50, AfgConstants.BicolorCap);
                else
                    cfg.BuyQuantityAmount = Math.Clamp(amount, 1, 99);
                cfg.SaveDebounced();
            }
        }

        ImGui.SameLine(0, 14f * ImGuiHelpers.GlobalScale);
        Caption("Keep");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(82f * ImGuiHelpers.GlobalScale);
        var reserve = cfg.KeepGemstonesReserve;
        if (ImGui.InputInt("##planner_gem_reserve", ref reserve, 50, 250))
        {
            cfg.KeepGemstonesReserve = Math.Clamp(reserve, 0, AfgConstants.BicolorCap);
            cfg.SaveDebounced();
        }
        ImGui.SameLine();
        Caption("in reserve");

        ImGui.SameLine(0, 14f * ImGuiHelpers.GlobalScale);
        Caption("After");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(132f * ImGuiHelpers.GlobalScale);
        var after = cfg.AfterTrade == AfterTradeAction.Resume ? 0 : 1;
        var afterLabels = new[] { "Resume farming", "Stop the run" };
        if (ImGui.Combo("##planner_after_trade", ref after, afterLabels, afterLabels.Length))
        {
            cfg.AfterTrade = after == 0 ? AfterTradeAction.Resume : AfterTradeAction.Stop;
            cfg.SaveDebounced();
        }
    }

    private static void DrawPreview(Configuration cfg)
    {
        var item = GemstoneItemPicker.Selected(cfg);
        if (item is null) return;
        var quantity = GemstoneCatalog.ComputeBuyQuantity(cfg.TradeThreshold, item.CostPerOne);
        var color = quantity > 0 ? Styling.TextDim : Styling.AccentRose;
        using (ImRaii.PushColor(ImGuiCol.Text, color))
            ImGui.TextUnformatted(quantity > 0
                ? $"At {cfg.TradeThreshold} gems, Parcae buys about {quantity} x {item.ItemName} for {quantity * item.CostPerOne} gems."
                : $"This threshold and reserve cannot afford {item.ItemName} at {item.CostPerOne} gems each.");
    }

    private static void Caption(string text)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, Styling.TextSecondary))
            ImGui.TextUnformatted(text);
    }
}
