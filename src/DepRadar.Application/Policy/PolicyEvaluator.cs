using DepRadar.Application.Risk;

namespace DepRadar.Application.Policy;

/// <summary>
/// Evaluates an assessed graph against a <see cref="RiskPolicy"/>, producing the set
/// of violations. Pure and source-agnostic: it works on the same
/// <see cref="GraphAssessment"/> whether it came from a live scan or the stateless
/// CLI analyzer.
/// </summary>
public static class PolicyEvaluator
{
    /// <summary>Returns the policy outcome: a pass flag plus every violation found.</summary>
    public static PolicyOutcome Evaluate(GraphAssessment assessment, RiskPolicy policy)
    {
        var violations = new List<PolicyViolation>();

        foreach (var node in assessment.Nodes)
        {
            var coordinate = $"{node.Package.Value}@{node.Version}";
            var level = node.Assessment.Score.Level;

            if (level >= policy.FailOn)
            {
                violations.Add(new PolicyViolation(coordinate, $"risk level {level} is at or above the {policy.FailOn} threshold"));
            }

            if (!policy.AllowDeprecated && node.Input.IsDeprecated)
            {
                violations.Add(new PolicyViolation(coordinate, "package version is deprecated"));
            }

            if (node.Input.ResolvedLicense is { } license)
            {
                var category = license.Classify();
                if (policy.ForbiddenLicenses.Contains(category))
                {
                    violations.Add(new PolicyViolation(coordinate, $"license {license.Identifier} is a forbidden {category} license"));
                }
            }
        }

        return new PolicyOutcome(violations.Count == 0, violations);
    }
}

/// <summary>A single policy breach: which package coordinate and why.</summary>
public sealed record PolicyViolation(string Package, string Reason);

/// <summary>The result of a policy evaluation.</summary>
public sealed record PolicyOutcome(bool Passed, IReadOnlyList<PolicyViolation> Violations);
