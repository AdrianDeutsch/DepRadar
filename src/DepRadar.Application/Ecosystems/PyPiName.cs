using System.Text.RegularExpressions;

namespace DepRadar.Application.Ecosystems;

/// <summary>
/// Normalizes a PyPI project name per PEP 503: lower-cased, with any run of
/// <c>-</c>, <c>_</c> or <c>.</c> collapsed to a single <c>-</c>. This is the
/// canonical form PyPI, OSV and pip compare on, so <c>Flask</c>, <c>python_dateutil</c>
/// and <c>python.dateutil</c> all resolve to one node.
/// </summary>
public static partial class PyPiName
{
    /// <summary>Returns the PEP 503 canonical form of a project name.</summary>
    public static string Normalize(string name) =>
        SeparatorRegex().Replace(name.Trim(), "-").ToLowerInvariant();

    [GeneratedRegex(@"[-_.]+", RegexOptions.Compiled)]
    private static partial Regex SeparatorRegex();
}
