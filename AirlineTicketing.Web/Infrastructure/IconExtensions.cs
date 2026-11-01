using System.Collections.Concurrent;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AirlineTicketing.Web.Infrastructure;

/// <summary>
/// Renders inline Lucide-style SVG icons so views stay free of markup noise.
/// </summary>
public static class IconExtensions
{
    private static readonly Dictionary<string, string> Paths = new(StringComparer.OrdinalIgnoreCase)
    {
        ["plane"] = "<path d=\"M17.8 19.2 16 11l3.5-3.5C21 6 21.5 4 21 3c-1-.5-3 0-4.5 1.5L13 8 4.8 6.2c-.5-.1-.9.1-1.1.5l-.3.5c-.2.5-.1 1 .3 1.3L9 12l-2 3H4l-1 1 3 2 2 3 1-1v-3l3-2 3.5 5.3c.3.4.8.5 1.3.3l.5-.2c.4-.3.6-.7.5-1.2z\" />",
        ["plane-takeoff"] = "<path d=\"M2 22h20\" /><path d=\"M6.4 17.4 4 17l-2-4 1.1-.6a2 2 0 0 1 1.8 0l.2.1a2 2 0 0 0 1.8 0L8 12 5 6l.9-.5a2 2 0 0 1 2.1.2l4 3a2 2 0 0 0 2.1.2l4.2-2.1a2.4 2.4 0 0 1 1.6-.1c.8.2 1.3 1.1 1 1.9l-1.9 6.9a2 2 0 0 1-.5.9l-5.6 5.6a2 2 0 0 1-1.4.6h-3.2a2 2 0 0 1-1.6-.8z\" />",
        ["plane-landing"] = "<path d=\"M2 22h20\" /><path d=\"M3.8 10.8 2 9l2-4.5 1.1.6c.6.3.9.8.9 1.4v1.2a2 2 0 0 0 1.1 1.8l.2.1a2 2 0 0 0 1.8 0L10 9l4.5 4.5 4.5-1.5 1.5 1.5-6 5-3-2-3 2L7 16.5l1.5-3-2-2-2.7 1.8z\" />",
        ["eye-off"] = "<path d=\"M10.7 5.1A10.7 10.7 0 0 1 21.9 11.7a1 1 0 0 1 0 .7 10.7 10.7 0 0 1-1.4 2.5\" /><path d=\"M14.1 14.2a3 3 0 0 1-4.2-4.2\" /><path d=\"M17.5 17.5A10.8 10.8 0 0 1 2.1 12.3a1 1 0 0 1 0-.7 10.8 10.8 0 0 1 4.4-5.1\" /><path d=\"m2 2 20 20\" />",
        ["id-card"] = "<rect x=\"2\" y=\"5\" width=\"20\" height=\"14\" rx=\"2\" /><path d=\"M16 10h2M16 14h2\" /><circle cx=\"9\" cy=\"11\" r=\"2\" /><path d=\"M6.2 15a3 3 0 0 1 5.6 0\" />",
        ["ticket-percent"] = "<path d=\"M2 9a3 3 0 1 1 0 6v2a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-2a3 3 0 1 1 0-6V7a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2z\" /><path d=\"M9 9h.01M15 9l-6 6M15 15h.01\" />",
        ["badge-check"] = "<path d=\"M3.9 8.6a4 4 0 0 1 4.7-4.8 4 4 0 0 1 6.8 0 4 4 0 0 1 4.8 4.8 4 4 0 0 1 0 6.7 4 4 0 0 1-4.8 4.8 4 4 0 0 1-6.7 0 4 4 0 0 1-4.8-4.8 4 4 0 0 1 0-6.7z\" /><path d=\"m9 12 2 2 4-4\" />",
        ["globe"] = "<circle cx=\"12\" cy=\"12\" r=\"10\" /><path d=\"M2 12h20\" /><path d=\"M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z\" />",
        ["chevron-down"] = "<path d=\"m6 9 6 6 6-6\" />",
        ["chevron-right"] = "<path d=\"m9 18 6-6-6-6\" />",
        ["chevron-left"] = "<path d=\"m15 18-6-6 6-6\" />",
        ["arrow-left"] = "<path d=\"m12 19-7-7 7-7M19 12H5\" />",
        ["arrow-right"] = "<path d=\"M5 12h14m-7-7 7 7-7 7\" />",
        ["calendar"] = "<rect x=\"3\" y=\"4\" width=\"18\" height=\"18\" rx=\"2\" /><path d=\"M16 2v4M8 2v4M3 10h18\" />",
        ["search"] = "<circle cx=\"11\" cy=\"11\" r=\"8\" /><path d=\"m21 21-4.3-4.3\" />",
        ["info"] = "<circle cx=\"12\" cy=\"12\" r=\"10\" /><path d=\"M12 16v-4M12 8h.01\" />",
        ["alert"] = "<path d=\"m21.7 18-8-14a2 2 0 0 0-3.4 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.7-3z\" /><path d=\"M12 9v4M12 17h.01\" />",
        ["clock"] = "<circle cx=\"12\" cy=\"12\" r=\"10\" /><path d=\"M12 6v6l4 2\" />",
        ["check-circle"] = "<path d=\"M22 11.1V12a10 10 0 1 1-5.9-9.1\" /><path d=\"m9 11 3 3L22 4\" />",
        ["circle"] = "<circle cx=\"12\" cy=\"12\" r=\"10\" />",
        ["check"] = "<path d=\"M20 6 9 17l-5-5\" />",
        ["refresh"] = "<path d=\"M21 12a9 9 0 1 1-2.6-6.4\" /><path d=\"M21 3v6h-6\" />",
        ["undo"] = "<path d=\"M3 7v6h6\" /><path d=\"M21 17a9 9 0 0 0-9-9 9 9 0 0 0-6 2.3L3 13\" />",
        ["lock"] = "<rect x=\"3\" y=\"11\" width=\"18\" height=\"11\" rx=\"2\" /><path d=\"M7 11V7a5 5 0 0 1 10 0v4\" />",
        ["credit-card"] = "<rect x=\"2\" y=\"5\" width=\"20\" height=\"14\" rx=\"2\" /><path d=\"M2 10h20\" />",
        ["users"] = "<path d=\"M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2\" /><circle cx=\"9\" cy=\"7\" r=\"4\" /><path d=\"M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75\" />",
        ["user"] = "<path d=\"M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2\" /><circle cx=\"12\" cy=\"7\" r=\"4\" />",
        ["shield-check"] = "<path d=\"M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z\" /><path d=\"m9 12 2 2 4-4\" />",
        ["ticket"] = "<path d=\"M2 9a3 3 0 0 1 0 6v2a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-2a3 3 0 0 1 0-6V7a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2z\" /><path d=\"M13 5v2M13 11v2M13 17v2\" />",
        ["inbox"] = "<path d=\"M22 12h-6l-2 3h-4l-2-3H2\" /><path d=\"M5.5 5.1 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.5-6.9A2 2 0 0 0 16.8 4H7.2a2 2 0 0 0-1.7 1.1z\" />",
        ["armchair"] = "<path d=\"M19 9V6a2 2 0 0 0-2-2H7a2 2 0 0 0-2 2v3\" /><path d=\"M3 16a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-1a2 2 0 0 0-2-2 2 2 0 0 0-2 2v1H7v-1a2 2 0 0 0-2-2 2 2 0 0 0-2 2z\" /><path d=\"M5 18v2M19 18v2\" />",
        ["luggage"] = "<path d=\"M6 20h12a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v10a2 2 0 0 0 2 2z\" /><path d=\"M9 6V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2M9 20v2M15 20v2\" />",
        ["headset"] = "<path d=\"M3 11a9 9 0 0 1 18 0\" /><path d=\"M21 16a2 2 0 0 1-2 2h-1a2 2 0 0 1-2-2v-3a2 2 0 0 1 2-2h3zM3 16a2 2 0 0 0 2 2h1a2 2 0 0 0 2-2v-3a2 2 0 0 0-2-2H3z\" /><path d=\"M21 16v2a3 3 0 0 1-3 3h-4\" />",
        ["sliders"] = "<path d=\"M4 21v-7M4 10V3M12 21v-9M12 8V3M20 21v-5M20 12V3\" /><path d=\"M1 14h6M9 8h6M17 16h6\" />",
        ["map-pin"] = "<path d=\"M20 10c0 6-8 12-8 12s-8-6-8-12a8 8 0 0 1 16 0z\" /><circle cx=\"12\" cy=\"10\" r=\"3\" />",
        ["x"] = "<path d=\"M18 6 6 18M6 6l12 12\" />",
        ["download"] = "<path d=\"M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3\" />",
        ["send"] = "<path d=\"m22 2-7 20-4-9-9-4z\" />",
        ["mail"] = "<rect x=\"2\" y=\"4\" width=\"20\" height=\"16\" rx=\"2\" /><path d=\"m22 7-10 5L2 7\" />",
        ["phone"] = "<path d=\"M22 16.9v3a2 2 0 0 1-2.2 2 19.8 19.8 0 0 1-8.6-3.1 19.5 19.5 0 0 1-6-6A19.8 19.8 0 0 1 2 4.2 2 2 0 0 1 4 2h3a2 2 0 0 1 2 1.7c.1.9.4 1.8.7 2.7a2 2 0 0 1-.5 2.1L8.1 9.9a16 16 0 0 0 6 6l1.4-1.1a2 2 0 0 1 2.1-.5c.9.3 1.8.6 2.7.7a2 2 0 0 1 1.7 2z\" />",
        ["plus"] = "<path d=\"M12 5v14M5 12h14\" />",
        ["minus"] = "<path d=\"M5 12h14\" />",
        ["building"] = "<rect x=\"4\" y=\"2\" width=\"16\" height=\"20\" rx=\"2\" /><path d=\"M9 22v-4h6v4M8 6h.01M16 6h.01M8 10h.01M16 10h.01M8 14h.01M16 14h.01\" />",
        ["chart"] = "<path d=\"M12 20V10M18 20V4M6 20v-4\" />",
        ["trending-up"] = "<path d=\"m22 7-8.5 8.5-5-5L2 17\" /><path d=\"M16 7h6v6\" />",
        ["log-out"] = "<path d=\"M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4\" /><path d=\"m16 17 5-5-5-5M21 12H9\" />",
        ["percent"] = "<path d=\"M19 5 5 19\" /><circle cx=\"6.5\" cy=\"6.5\" r=\"2.5\" /><circle cx=\"17.5\" cy=\"17.5\" r=\"2.5\" />",
        ["wallet"] = "<path d=\"M20 12V8a2 2 0 0 0-2-2H4a2 2 0 0 0 0 4h16a2 2 0 0 1 2 2v6a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V6\" />",
        ["bell"] = "<path d=\"M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9\" /><path d=\"M13.7 21a2 2 0 0 1-3.4 0\" />",
        ["help"] = "<circle cx=\"12\" cy=\"12\" r=\"10\" /><path d=\"M9.1 9a3 3 0 0 1 5.8 1c0 2-3 3-3 3\" /><path d=\"M12 17h.01\" />",
        ["activity"] = "<path d=\"M22 12h-4l-3 9L9 3l-3 9H2\" />",
        ["layout-dashboard"] = "<rect x=\"3\" y=\"3\" width=\"7\" height=\"9\" rx=\"1\" /><rect x=\"14\" y=\"3\" width=\"7\" height=\"5\" rx=\"1\" /><rect x=\"14\" y=\"12\" width=\"7\" height=\"9\" rx=\"1\" /><rect x=\"3\" y=\"16\" width=\"7\" height=\"5\" rx=\"1\" />",
        ["shield-alert"] = "<path d=\"M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z\" /><path d=\"M12 8v4M12 16h.01\" />",
        ["database"] = "<ellipse cx=\"12\" cy=\"5\" rx=\"9\" ry=\"3\" /><path d=\"M21 12c0 1.7-4 3-9 3s-9-1.3-9-3\" /><path d=\"M3 5v14c0 1.7 4 3 9 3s9-1.3 9-3V5\" />",
        ["settings"] = "<circle cx=\"12\" cy=\"12\" r=\"3\" /><path d=\"M19.4 15a1.7 1.7 0 0 0 .3 1.9l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.9-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1-1.5 1.7 1.7 0 0 0-1.9.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.9 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1 1.7 1.7 0 0 0-.3-1.9l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.9.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.9-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.9V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z\" />",
        ["scroll-text"] = "<path d=\"M15 12h-5M15 8h-5\" /><path d=\"M19 17V5a2 2 0 0 0-2-2H4\" /><path d=\"M8 21h12a2 2 0 0 0 2-2v-1a2 2 0 0 0-2-2H10a2 2 0 0 0-2 2v1a2 2 0 0 0-2 2z\" />",
    };

    private static readonly ConcurrentDictionary<string, bool> Warned = new();

    public static IHtmlContent Icon(this IHtmlHelper html, string name, int size = 17, string? cssClass = null)
    {
        if (!Paths.TryGetValue(name, out var inner))
        {
            if (Warned.TryAdd(name, true))
            {
                System.Diagnostics.Debug.WriteLine($"[Icons] Unknown icon '{name}'.");
            }

            inner = "<circle cx=\"12\" cy=\"12\" r=\"10\" />";
        }

        var cls = string.IsNullOrWhiteSpace(cssClass) ? "icon" : $"icon {cssClass}";
        var markup =
            $"<svg class=\"{cls}\" width=\"{size}\" height=\"{size}\" viewBox=\"0 0 24 24\" fill=\"none\" " +
            "stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" " +
            $"aria-hidden=\"true\" focusable=\"false\">{inner}</svg>";

        return new HtmlString(markup);
    }
}
