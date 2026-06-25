using DepRadar.Application.Risk;
using DepRadar.Domain.Risk;

namespace DepRadar.Application.Chat;

/// <summary>
/// Answers natural-language questions about a scanned graph deterministically, by
/// matching the question to an intent and querying the assessed nodes. This is the
/// keyless default; a configured LLM can produce a richer narrative over the same data.
/// Pure and unit-tested.
/// </summary>
public static class GraphQuestionAnswerer
{
    /// <summary>Answers <paramref name="question"/> over the assessed graph nodes.</summary>
    public static ChatAnswer Answer(string question, IReadOnlyList<AssessedNode> nodes)
    {
        var q = (question ?? string.Empty).ToLowerInvariant();

        if (ContainsAny(q, "deprecat"))
        {
            return ByPredicate(nodes, HasCode("DEPRECATED"), "are deprecated on NuGet");
        }

        if (ContainsAny(q, "vuln", "cve", "security", "advisor", "exploit"))
        {
            return ByPredicate(nodes, HasCategory(RiskCategory.Security), "have a known security advisory");
        }

        if (ContainsAny(q, "archiv", "abandon", "unmaintain", "stale", "maintain"))
        {
            return ByPredicate(nodes, node => HasCode("ARCHIVED")(node) || HasCode("STALE")(node), "have an unmaintained or archived repository");
        }

        if (ContainsAny(q, "copyleft", "gpl", "licens"))
        {
            return ByPredicate(nodes, node => HasCategory(RiskCategory.License)(node) || HasCategory(RiskCategory.LicenseShift)(node), "carry a license concern");
        }

        if (ContainsAny(q, "riskiest", "worst", "most risk", "highest", "biggest"))
        {
            return Worst(nodes);
        }

        return Summary(nodes);
    }

    private static bool ContainsAny(string text, params string[] tokens) =>
        tokens.Any(token => text.Contains(token, StringComparison.Ordinal));

    private static Func<AssessedNode, bool> HasCode(string code) =>
        node => node.Assessment.Findings.Any(finding => finding.Code == code);

    private static Func<AssessedNode, bool> HasCategory(RiskCategory category) =>
        node => node.Assessment.Findings.Any(finding => finding.Category == category);

    private static ChatAnswer ByPredicate(IReadOnlyList<AssessedNode> nodes, Func<AssessedNode, bool> predicate, string phrase)
    {
        var matched = nodes.Where(predicate).Select(node => node.Package.Original).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var text = matched.Count == 0
            ? $"None of the {nodes.Count} packages {phrase}."
            : $"{matched.Count} of {nodes.Count} packages {phrase}: {string.Join(", ", matched)}.";
        return new ChatAnswer(text, matched);
    }

    private static ChatAnswer Worst(IReadOnlyList<AssessedNode> nodes)
    {
        if (nodes.Count == 0)
        {
            return new ChatAnswer("No packages have been scanned yet.", []);
        }

        var worst = nodes.MinBy(node => node.Assessment.Score.Value)!;
        var codes = worst.Assessment.Findings.Select(f => f.Code).Distinct();
        var detail = worst.Assessment.Findings.Count == 0 ? "no findings" : string.Join(", ", codes);
        var text = $"The riskiest package is {worst.Package.Original} {worst.Version} — health {worst.Assessment.Score.Value}/100 ({worst.Assessment.Score.Level}); {detail}.";
        return new ChatAnswer(text, [worst.Package.Original]);
    }

    private static ChatAnswer Summary(IReadOnlyList<AssessedNode> nodes)
    {
        if (nodes.Count == 0)
        {
            return new ChatAnswer("No packages have been scanned yet.", []);
        }

        var withFindings = nodes.Where(node => node.Assessment.Findings.Count > 0).ToList();
        var worstLevel = nodes.Max(node => node.Assessment.Score.Level);
        var text = $"Scanned {nodes.Count} packages; worst risk level is {worstLevel}. {withFindings.Count} have findings. " +
                   "Ask about deprecated, vulnerable, copyleft or unmaintained packages, or the riskiest one.";
        return new ChatAnswer(text, withFindings.Select(node => node.Package.Original).Take(10).ToList());
    }
}

/// <summary>An answer plus the package ids it refers to (for highlighting).</summary>
public sealed record ChatAnswer(string Text, IReadOnlyList<string> Packages);
