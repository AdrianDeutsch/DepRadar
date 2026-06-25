using DepRadar.Domain.ValueObjects;

namespace DepRadar.Domain.Risk;

/// <summary>
/// Pure scoring logic: turns a package's signals into explainable findings and an
/// aggregate health score. No I/O, no framework types — this is the heart of the
/// product and is covered directly by unit tests.
/// </summary>
public static class PackageRiskScorer
{
    /// <summary>Assesses a single package version.</summary>
    public static RiskAssessment Assess(PackageRiskInput input)
    {
        var findings = new List<RiskFinding>();

        AddSecurityFindings(findings, input);
        AddLicenseFindings(findings, input.ResolvedLicense);
        AddLicenseShiftFinding(findings, input.ResolvedLicense, input.LatestLicense);
        AddMaintenanceFindings(findings, input);

        return new RiskAssessment(HealthScore.FromFindings(findings), findings);
    }

    private static void AddSecurityFindings(List<RiskFinding> findings, PackageRiskInput input)
    {
        foreach (var vulnerability in input.Vulnerabilities)
        {
            var summary = string.IsNullOrWhiteSpace(vulnerability.Summary)
                ? vulnerability.AdvisoryId
                : $"{vulnerability.AdvisoryId}: {vulnerability.Summary}";

            findings.Add(new RiskFinding(RiskCategory.Security, vulnerability.Severity, "VULN", summary));
        }
    }

    private static void AddLicenseFindings(List<RiskFinding> findings, SpdxLicense? license)
    {
        if (license is null)
        {
            findings.Add(new RiskFinding(RiskCategory.License, RiskLevel.Low, "LICENSE_UNKNOWN", "License could not be determined."));
            return;
        }

        switch (license.Value.Classify())
        {
            case LicenseCategory.Copyleft:
                findings.Add(new RiskFinding(RiskCategory.License, RiskLevel.High, "COPYLEFT", $"Strong copyleft license ({license}) imposes viral obligations."));
                break;
            case LicenseCategory.WeakCopyleft:
                findings.Add(new RiskFinding(RiskCategory.License, RiskLevel.Medium, "WEAK_COPYLEFT", $"Weak copyleft license ({license}) imposes file/library-level obligations."));
                break;
            case LicenseCategory.Unknown:
                findings.Add(new RiskFinding(RiskCategory.License, RiskLevel.Medium, "LICENSE_NONSTANDARD", $"Non-OSI or unrecognized license ({license})."));
                break;
            case LicenseCategory.Permissive:
            default:
                break;
        }
    }

    private static void AddLicenseShiftFinding(List<RiskFinding> findings, SpdxLicense? resolved, SpdxLicense? latest)
    {
        if (resolved is not { } from || latest is not { } to)
        {
            return;
        }

        if (string.Equals(from.Identifier, to.Identifier, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // A shift toward a more restrictive (or unknown/commercial) license is the
        // "MediatR case": you are on an OSS version while newer ones tightened terms.
        var tightened = to.Classify() > from.Classify();
        var level = tightened ? RiskLevel.High : RiskLevel.Medium;
        var note = tightened ? " — newer versions tightened the license (possible commercialization)" : string.Empty;

        findings.Add(new RiskFinding(
            RiskCategory.LicenseShift,
            level,
            "LICENSE_SHIFT",
            $"License changed from {from} to {to} in newer versions{note}."));
    }

    private static void AddMaintenanceFindings(List<RiskFinding> findings, PackageRiskInput input)
    {
        if (input.IsDeprecated)
        {
            findings.Add(new RiskFinding(RiskCategory.Maintenance, RiskLevel.High, "DEPRECATED", "The package version is deprecated on NuGet."));
        }

        if (input.IsArchived)
        {
            findings.Add(new RiskFinding(RiskCategory.Maintenance, RiskLevel.High, "ARCHIVED", "The source repository is archived (no longer maintained)."));
        }
        else if (input.IsRepositoryStale)
        {
            findings.Add(new RiskFinding(RiskCategory.Maintenance, RiskLevel.Medium, "STALE", "No recent commits to the source repository."));
        }
    }
}
