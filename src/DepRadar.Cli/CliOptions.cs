using DepRadar.Application.Policy;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Cli;

/// <summary>Parsed <c>scan</c> command options.</summary>
/// <param name="Target">A package id, a <c>.csproj</c>, or a <c>packages.lock.json</c> path.</param>
/// <param name="FailOn">The level at or above which a package fails the policy.</param>
/// <param name="AllowDeprecated">Whether deprecated packages are tolerated.</param>
/// <param name="ForbiddenLicenses">License categories that fail the policy.</param>
/// <param name="SbomPath">Optional path to also write a CycloneDX SBOM.</param>
/// <param name="SarifPath">Optional path to also write a SARIF report (for code scanning).</param>
/// <param name="PolicyPath">Optional policy-as-code file (defaults to ./depradar.json if present).</param>
/// <param name="Json">Whether to emit machine-readable JSON.</param>
internal sealed record CliOptions(
    string Target,
    RiskLevel FailOn,
    bool AllowDeprecated,
    IReadOnlySet<LicenseCategory> ForbiddenLicenses,
    string? SbomPath,
    string? SarifPath,
    string? PolicyPath,
    bool Json)
{
    /// <summary>The usage banner.</summary>
    public const string Usage = """
        Usage: depradar scan <package-id | .csproj | packages.lock.json> [options]

        Options:
          --fail-on <none|low|medium|high|critical>            Fail when any package is at/above this level (default: high)
          --no-deprecated                                      Fail when any package is deprecated
          --forbid <permissive|weakcopyleft|copyleft|unknown>  Forbid a license category (repeatable)
          --policy <path>                                      Read the gate from a policy file (else ./depradar.json)
          --sbom <path>                                        Also write a CycloneDX SBOM to <path>
          --sarif <path>                                       Also write a SARIF report to <path> (GitHub code scanning)
          --json                                               Emit JSON instead of text

        Exit codes: 0 = policy passed, 1 = policy violated, 2 = usage error.
        """;

    /// <summary>Builds the policy this run gates on.</summary>
    public RiskPolicy ToPolicy() => new(FailOn, AllowDeprecated, ForbiddenLicenses);

    /// <summary>Parses argv (after the <c>scan</c> verb). Returns <see langword="false"/> with a message on error.</summary>
    public static bool TryParse(string[] args, out CliOptions? options, out string? error)
    {
        options = null;
        error = null;

        string? target = null;
        var failOn = RiskLevel.High;
        var allowDeprecated = true;
        var forbidden = new HashSet<LicenseCategory>();
        string? sbomPath = null;
        string? sarifPath = null;
        string? policyPath = null;
        var json = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--fail-on":
                    if (!TryTakeValue(args, ref i, out var levelText) || !Enum.TryParse(levelText, ignoreCase: true, out failOn))
                    {
                        error = $"Invalid --fail-on value. Expected one of: {string.Join(", ", Enum.GetNames<RiskLevel>())}.";
                        return false;
                    }

                    break;

                case "--no-deprecated":
                    allowDeprecated = false;
                    break;

                case "--forbid":
                    if (!TryTakeValue(args, ref i, out var categoryText) || !Enum.TryParse<LicenseCategory>(categoryText, ignoreCase: true, out var category))
                    {
                        error = $"Invalid --forbid value. Expected one of: {string.Join(", ", Enum.GetNames<LicenseCategory>())}.";
                        return false;
                    }

                    forbidden.Add(category);
                    break;

                case "--sbom":
                    if (!TryTakeValue(args, ref i, out sbomPath))
                    {
                        error = "--sbom requires a file path.";
                        return false;
                    }

                    break;

                case "--sarif":
                    if (!TryTakeValue(args, ref i, out sarifPath))
                    {
                        error = "--sarif requires a file path.";
                        return false;
                    }

                    break;

                case "--policy":
                    if (!TryTakeValue(args, ref i, out policyPath))
                    {
                        error = "--policy requires a file path.";
                        return false;
                    }

                    break;

                case "--json":
                    json = true;
                    break;

                default:
                    if (arg.StartsWith('-'))
                    {
                        error = $"Unknown option '{arg}'.";
                        return false;
                    }

                    if (target is not null)
                    {
                        error = "Specify exactly one target (package id or project file).";
                        return false;
                    }

                    target = arg;
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            error = "Missing target.";
            return false;
        }

        options = new CliOptions(target, failOn, allowDeprecated, forbidden, sbomPath, sarifPath, policyPath, json);
        return true;
    }

    private static bool TryTakeValue(string[] args, ref int index, out string value)
    {
        if (index + 1 < args.Length)
        {
            value = args[++index];
            return true;
        }

        value = string.Empty;
        return false;
    }
}
