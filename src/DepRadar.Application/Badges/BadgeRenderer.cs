using System.Globalization;
using DepRadar.Domain.Risk;

namespace DepRadar.Application.Badges;

/// <summary>
/// Renders a flat, shields.io-style SVG status badge for a package's health — drop it
/// in a README to advertise dependency health at a glance.
/// </summary>
public static class BadgeRenderer
{
    private const string Label = "DepRadar";

    /// <summary>A health badge: score + a one-word verdict, coloured by risk level.</summary>
    public static string RenderHealth(int score, RiskLevel level)
    {
        var value = string.Create(CultureInfo.InvariantCulture, $"{score} {Verdict(level)}");
        return Render(value, Color(level));
    }

    /// <summary>A neutral badge for a package that has not been scanned.</summary>
    public static string RenderUnknown() => Render("not scanned", "#9f9f9f");

    private static string Render(string value, string color)
    {
        // Approximate text widths (no font metrics available server-side).
        var labelWidth = (Label.Length * 7) + 12;
        var valueWidth = (value.Length * 7) + 12;
        var total = labelWidth + valueWidth;
        var labelMid = labelWidth / 2;
        var valueMid = labelWidth + (valueWidth / 2);
        var safeValue = Escape(value);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="{total}" height="20" role="img" aria-label="{Label}: {safeValue}">
              <linearGradient id="s" x2="0" y2="100%"><stop offset="0" stop-color="#bbb" stop-opacity=".1"/><stop offset="1" stop-opacity=".1"/></linearGradient>
              <clipPath id="r"><rect width="{total}" height="20" rx="3" fill="#fff"/></clipPath>
              <g clip-path="url(#r)">
                <rect width="{labelWidth}" height="20" fill="#555"/>
                <rect x="{labelWidth}" width="{valueWidth}" height="20" fill="{color}"/>
                <rect width="{total}" height="20" fill="url(#s)"/>
              </g>
              <g fill="#fff" text-anchor="middle" font-family="Verdana,Geneva,DejaVu Sans,sans-serif" font-size="11">
                <text x="{labelMid}" y="15" fill="#010101" fill-opacity=".3">{Label}</text>
                <text x="{labelMid}" y="14">{Label}</text>
                <text x="{valueMid}" y="15" fill="#010101" fill-opacity=".3">{safeValue}</text>
                <text x="{valueMid}" y="14">{safeValue}</text>
              </g>
            </svg>
            """);
    }

    private static string Verdict(RiskLevel level) => level switch
    {
        RiskLevel.Critical => "critical",
        RiskLevel.High => "risky",
        RiskLevel.Medium => "caution",
        _ => "healthy",
    };

    private static string Color(RiskLevel level) => level switch
    {
        RiskLevel.Critical => "#e05d44",
        RiskLevel.High => "#fe7d37",
        RiskLevel.Medium => "#dfb317",
        _ => "#4c1",
    };

    private static string Escape(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
}
