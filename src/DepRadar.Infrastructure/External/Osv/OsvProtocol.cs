using System.Net.Http.Json;
using System.Text.Json;
using DepRadar.Application.Abstractions;
using DepRadar.Domain.Risk;
using DepRadar.Domain.ValueObjects;
using DepRadar.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace DepRadar.Infrastructure.External.Osv;

/// <summary>Parses an ecosystem's version string into the Domain <see cref="SemVer"/>.</summary>
internal delegate bool TryParseVersion(string? value, out SemVer version);

/// <summary>
/// The OSV.dev wire protocol, shared by every ecosystem's vulnerability source —
/// OSV is multi-ecosystem by design, so the NuGet/npm/PyPI adapters differ only in
/// the ecosystem name and the version grammar. Responses are cached so re-scans
/// don't re-query OSV.
/// </summary>
internal static class OsvProtocol
{
    private const string SourceName = "OSV";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Queries <c>/v1/query</c> for the advisories affecting one package version.</summary>
    public static async Task<IReadOnlyList<VulnerabilityRecord>> QueryAsync(
        HttpClient httpClient,
        HybridCache cache,
        string ecosystem,
        string packageName,
        SemVer version,
        CancellationToken cancellationToken)
    {
        var json = await cache.GetOrCreateAsync(
            $"osv:{ecosystem}:{packageName}:{version}",
            (httpClient, ecosystem, packageName, version),
            static async (state, token) =>
            {
                var request = new OsvQueryRequest(new OsvPackage(state.packageName, state.ecosystem), state.version.ToString());
                using var response = await state.httpClient.PostAsJsonAsync("v1/query", request, JsonOptions, token);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(token);
            },
            HttpJsonCache.Options,
            cancellationToken: cancellationToken);

        var payload = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<OsvQueryResponse>(json, JsonOptions);
        if (payload?.Vulns is null)
        {
            return [];
        }

        return payload.Vulns
            .Where(vulnerability => !string.IsNullOrWhiteSpace(vulnerability.Id))
            .Select(vulnerability => new VulnerabilityRecord(
                vulnerability.Id!,
                MapSeverity(vulnerability.DatabaseSpecific?.Severity),
                Summarize(vulnerability),
                SourceName,
                CveOf(vulnerability)))
            .ToList();
    }

    /// <summary>
    /// Reads the advisory from <c>/v1/vulns/{id}</c> and returns the smallest
    /// <c>fixed</c> version above <paramref name="aboveVersion"/> for the package,
    /// or null when the advisory has no applicable patched release.
    /// </summary>
    public static async Task<string?> FindFixedVersionAsync(
        HttpClient httpClient,
        HybridCache cache,
        string ecosystem,
        string packageName,
        string advisoryId,
        SemVer aboveVersion,
        TryParseVersion tryParseVersion,
        CancellationToken cancellationToken)
    {
        var json = await cache.GetOrCreateAsync(
            $"osv-vuln:{advisoryId}",
            (httpClient, advisoryId),
            static async (state, token) =>
            {
                using var response = await state.httpClient.GetAsync($"v1/vulns/{Uri.EscapeDataString(state.advisoryId)}", token);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(token);
            },
            HttpJsonCache.Options,
            cancellationToken: cancellationToken);

        var advisory = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<OsvAdvisory>(json, JsonOptions);
        if (advisory?.Affected is null)
        {
            return null;
        }

        // OSV stores the ecosystem's canonical package casing, so match case-insensitively,
        // then take the smallest "fixed" version above the current one.
        return advisory.Affected
            .Where(affected => string.Equals(affected.Package?.Ecosystem, ecosystem, StringComparison.OrdinalIgnoreCase)
                && string.Equals(affected.Package?.Name, packageName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(affected => affected.Ranges ?? [])
            .SelectMany(range => range.Events ?? [])
            .Select(rangeEvent => rangeEvent.Fixed)
            .Select(fixedVersion => tryParseVersion(fixedVersion, out var parsed) ? parsed : null)
            .Where(fixedVersion => fixedVersion is not null && fixedVersion.CompareTo(aboveVersion) > 0)
            .OrderBy(fixedVersion => fixedVersion)
            .FirstOrDefault()?
            .ToString();
    }

    // GitHub-reviewed OSV advisories carry a LOW/MODERATE/HIGH/CRITICAL label.
    // A present-but-unlabeled advisory is treated as Medium rather than ignored.
    private static RiskLevel MapSeverity(string? label) => label?.ToUpperInvariant() switch
    {
        "CRITICAL" => RiskLevel.Critical,
        "HIGH" => RiskLevel.High,
        "MODERATE" or "MEDIUM" => RiskLevel.Medium,
        "LOW" => RiskLevel.Low,
        _ => RiskLevel.Medium,
    };

    // OSV keys GitHub-reviewed advisories by GHSA id; the CVE sits in the aliases.
    private static string? CveOf(OsvVulnerability vulnerability) =>
        vulnerability.Id!.StartsWith("CVE-", StringComparison.OrdinalIgnoreCase)
            ? vulnerability.Id
            : vulnerability.Aliases?.FirstOrDefault(alias => alias.StartsWith("CVE-", StringComparison.OrdinalIgnoreCase));

    private static string Summarize(OsvVulnerability vulnerability)
    {
        var text = !string.IsNullOrWhiteSpace(vulnerability.Summary)
            ? vulnerability.Summary
            : vulnerability.Aliases is { Count: > 0 } aliases ? string.Join(", ", aliases) : string.Empty;

        return text.Length > 300 ? text[..300] : text;
    }
}
