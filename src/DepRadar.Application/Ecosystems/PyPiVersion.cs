using System.Globalization;
using System.Text.RegularExpressions;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Ecosystems;

/// <summary>
/// Normalizes a PEP 440 release string into the Domain <see cref="SemVer"/>.
/// </summary>
/// <remarks>
/// PEP 440 is not SemVer: releases may have any number of numeric segments
/// (<c>1</c>, <c>1.4</c>, <c>1.4.2.3</c>) and may carry epoch, pre-, post-, dev-
/// or local suffixes. Only <em>final</em> releases are accepted; pre/post/dev/local
/// versions and non-zero epochs are rejected so dependency resolution targets stable
/// releases. The numeric release is padded to <c>major.minor.patch</c> (NuGet's fourth
/// component carries a fourth segment). See ADR 0017.
/// </remarks>
public static partial class PyPiVersion
{
    /// <summary>
    /// Attempts to parse a final PEP 440 release; <paramref name="version"/> is meaningful
    /// only when it returns true. Pre/post/dev/local releases return false by design.
    /// </summary>
    public static bool TryParse(string? value, out SemVer version)
    {
        version = null!;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();

        // Drop a zero epoch ("0!1.2"); a real epoch changes ordering across the rest of
        // the grammar, so such versions are skipped rather than mis-ranked.
        var epoch = text.IndexOf('!', StringComparison.Ordinal);
        if (epoch >= 0)
        {
            if (text[..epoch] is not "0")
            {
                return false;
            }

            text = text[(epoch + 1)..];
        }

        // Accept only a pure dotted-numeric release (rejects a/b/rc/.post/.dev/+local).
        if (!ReleaseRegex().IsMatch(text))
        {
            return false;
        }

        var segments = text.Split('.', StringSplitOptions.RemoveEmptyEntries);

        // SemVer carries up to four numeric components; pad to three, take at most four.
        var major = Segment(segments, 0);
        var minor = Segment(segments, 1);
        var patch = Segment(segments, 2);
        var revision = Segment(segments, 3);

        var normalized = revision > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{major}.{minor}.{patch}.{revision}")
            : string.Create(CultureInfo.InvariantCulture, $"{major}.{minor}.{patch}");

        return SemVer.TryParse(normalized, out version);
    }

    private static int Segment(string[] segments, int index) =>
        index < segments.Length && int.TryParse(segments[index], NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;

    [GeneratedRegex(@"^\d+(\.\d+)*$", RegexOptions.Compiled)]
    private static partial Regex ReleaseRegex();
}
