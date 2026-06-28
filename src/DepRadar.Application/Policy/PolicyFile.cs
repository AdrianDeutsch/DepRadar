using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Application.Policy;

/// <summary>
/// Policy-as-code: reads a <c>depradar.json</c> file into a <see cref="RiskPolicy"/>, so
/// the gate lives in the repo next to the code instead of in CI flags. Pure — no I/O.
/// </summary>
public static class PolicyFile
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Parses the policy document. Unknown/missing fields fall back to lenient defaults.</summary>
    /// <exception cref="FormatException">The content is not valid policy JSON.</exception>
    public static RiskPolicy Parse(string json)
    {
        PolicyDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<PolicyDocument>(json, Options);
        }
        catch (JsonException exception)
        {
            throw new FormatException("Invalid depradar policy file.", exception);
        }

        document ??= new PolicyDocument();

        return new RiskPolicy(
            document.FailOn ?? RiskLevel.High,
            document.AllowDeprecated ?? true,
            (document.ForbiddenLicenses ?? []).ToFrozenSet())
        {
            IgnoredPackages = (document.Ignore ?? [])
                .Select(id => id.Trim())
                .Where(id => id.Length > 0)
                .ToFrozenSet(StringComparer.OrdinalIgnoreCase),
        };
    }

    private sealed record PolicyDocument
    {
        public RiskLevel? FailOn { get; init; }

        public bool? AllowDeprecated { get; init; }

        public IReadOnlyList<LicenseCategory>? ForbiddenLicenses { get; init; }

        public IReadOnlyList<string>? Ignore { get; init; }
    }
}
