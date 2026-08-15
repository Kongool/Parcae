using AutoFateGrind.Core.External;
using AutoFateGrind.Core.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;

namespace AutoFateGrind.Windows.Sections;

/// <summary>
/// Persistent Parcae identity and navigation surface shared by idle and active runs.
/// The three-node mark represents the route, encounter, and reward stages of a FATE run.
/// </summary>
internal static class ParcaeHeader
{
    private const float HeaderHeight = 92f;

    public static void Draw(Plugin plugin, AutoFateController controller)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(ImGui.GetContentRegionAvail().X, HeaderHeight * scale);
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var draw = ImGui.GetWindowDrawList();

        var left = Vector4.Lerp(Styling.CardBg, Styling.AccentViolet, 0.15f);
        var right = Vector4.Lerp(Styling.CardBg, Styling.AccentBlue, 0.07f);
        draw.AddRectFilledMultiColor(origin, end,
            ImGui.GetColorU32(left), ImGui.GetColorU32(right),
            ImGui.GetColorU32(right), ImGui.GetColorU32(left));
        draw.AddRect(origin, end, ImGui.GetColorU32(Styling.WithAlpha(Styling.AccentVioletSoft, 0.38f)),
            Styling.CardRounding, ImDrawFlags.None, 1.2f * scale);

        DrawMark(origin + new Vector2(39f, 45f) * scale, scale, draw);

        var textX = origin.X + 78f * scale;
        PutScaled("PARCAE", new Vector2(textX, origin.Y + 18f * scale), Styling.TextStrong, 1.62f);
        PutScaled("FATE OPERATIONS", new Vector2(textX, origin.Y + 51f * scale), Styling.AccentVioletSoft, 0.78f);

        var (status, detail, accent) = ResolveStatus(controller);
        DrawStatusPill(end.X - 18f * scale, origin.Y + 17f * scale, status, accent, scale, draw);
        PutRight(detail, end.X - 18f * scale, origin.Y + 51f * scale, Styling.TextDim, 0.82f);

        DrawActions(plugin, end.X - 18f * scale, end.Y - 30f * scale, scale);

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(size);
    }

    private static void DrawMark(Vector2 center, float scale, ImDrawListPtr draw)
    {
        var phase = Styling.Phase(Styling.PulseOrbit) * MathF.PI * 2f;
        var orbit = 17f * scale;
        var colors = new[] { Styling.AccentVioletSoft, Styling.AccentPink, Styling.AccentBlueSoft };
        var points = new Vector2[3];

        for (var i = 0; i < points.Length; i++)
        {
            var angle = phase + i * MathF.PI * 2f / points.Length;
            points[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * orbit;
        }

        for (var i = 0; i < points.Length; i++)
            draw.AddLine(points[i], points[(i + 1) % points.Length],
                ImGui.GetColorU32(Styling.WithAlpha(colors[i], 0.45f)), 1.2f * scale);

        draw.AddCircleFilled(center, 9f * scale,
            ImGui.GetColorU32(Vector4.Lerp(Styling.CardBg, Styling.AccentViolet, 0.55f)));
        draw.AddCircle(center, 9f * scale, ImGui.GetColorU32(Styling.AccentVioletSoft), 24, 1.5f * scale);

        for (var i = 0; i < points.Length; i++)
        {
            draw.AddCircleFilled(points[i], 5.5f * scale,
                ImGui.GetColorU32(Styling.WithAlpha(colors[i], 0.18f)));
            draw.AddCircleFilled(points[i], 2.7f * scale, ImGui.GetColorU32(colors[i]));
        }
    }

    private static (string Status, string Detail, Vector4 Accent) ResolveStatus(AutoFateController controller)
    {
        if (!ExternalPlugins.AllRequiredInstalled())
            return ("SETUP", "dependencies required", Styling.AccentRose);
        if (controller.Paused)
            return ("PAUSED", controller.Status, Styling.AccentAmber);
        if (controller.Running)
            return ("WEAVING", controller.Status, Styling.AccentMint);
        return ("READY", "waiting for a route", Styling.AccentVioletSoft);
    }

    private static void DrawStatusPill(float rightX, float y, string label, Vector4 accent, float scale, ImDrawListPtr draw)
    {
        ImGui.SetWindowFontScale(0.78f);
        var textSize = ImGui.CalcTextSize(label);
        ImGui.SetWindowFontScale(1f);

        var padX = 10f * scale;
        var height = 24f * scale;
        var width = textSize.X + padX * 2f + 8f * scale;
        var origin = new Vector2(rightX - width, y);
        var end = origin + new Vector2(width, height);
        var dot = origin + new Vector2(9f * scale, height * 0.5f);

        draw.AddRectFilled(origin, end, ImGui.GetColorU32(Vector4.Lerp(Styling.CardBg, accent, 0.16f)), height * 0.5f);
        draw.AddRect(origin, end, ImGui.GetColorU32(Styling.WithAlpha(accent, 0.55f)), height * 0.5f);
        draw.AddCircleFilled(dot, 2.8f * scale, ImGui.GetColorU32(accent));

        PutScaled(label, new Vector2(dot.X + 7f * scale, y + (height - textSize.Y) * 0.5f), accent, 0.78f);
    }

    private static void DrawActions(Plugin plugin, float rightX, float y, float scale)
    {
        var actions = new (FontAwesomeIcon Icon, string Id, string Tip, Action Action)[]
        {
            (FontAwesomeIcon.ChartLine, "history", "Run history", plugin.ToggleHistoryUi),
            (FontAwesomeIcon.Plug, "deps", "Dependencies", plugin.ToggleDependenciesUi),
            (FontAwesomeIcon.InfoCircle, "about", "About Parcae", plugin.ToggleAboutUi),
            (FontAwesomeIcon.Cog, "settings", "Settings", plugin.ToggleConfigUi),
        };

        var button = 24f * scale;
        var gap = 5f * scale;
        var x = rightX - actions.Length * button - (actions.Length - 1) * gap;

        foreach (var action in actions)
        {
            ImGui.SetCursorScreenPos(new Vector2(x, y));
            using (ImRaii.PushFont(UiBuilder.IconFont))
            using (ImRaii.PushColor(ImGuiCol.Button, Styling.CardBgSoft))
            using (ImRaii.PushColor(ImGuiCol.ButtonHovered, Styling.CardBgHover))
            using (ImRaii.PushColor(ImGuiCol.ButtonActive, Styling.AccentViolet * 0.55f))
            using (ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, 6f * scale))
                if (ImGui.Button($"{action.Icon.ToIconString()}##parcae_{action.Id}", new Vector2(button, button)))
                    action.Action();

            if (ImGui.IsItemHovered()) ImGui.SetTooltip(action.Tip);
            x += button + gap;
        }
    }

    private static void PutScaled(string text, Vector2 position, Vector4 color, float scale)
    {
        ImGui.SetWindowFontScale(scale);
        ImGui.SetCursorScreenPos(position);
        using (ImRaii.PushColor(ImGuiCol.Text, color))
            ImGui.TextUnformatted(text);
        ImGui.SetWindowFontScale(1f);
    }

    private static void PutRight(string text, float rightX, float y, Vector4 color, float scale)
    {
        ImGui.SetWindowFontScale(scale);
        var width = ImGui.CalcTextSize(text).X;
        ImGui.SetWindowFontScale(1f);
        PutScaled(text, new Vector2(rightX - width, y), color, scale);
    }
}
