using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DepRadar.Application.Abstractions;
using DepRadar.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace DepRadar.Infrastructure.External.DepsDev;

/// <summary>
/// <see cref="IPackageMetadataSource"/> backed by Google's deps.dev v3 API — the
/// recommended primary source: it aggregates versions, licenses and links across
/// ecosystems, so Slice 1 needs only two cheap calls per package.
/// </summary>
/// <remarks>
/// Resilience (retry, circuit breaker, timeout, rate-limiter) is attached to the
/// injected <see cref="HttpClient"/> in <c>AddInfrastructure</c>, so this class
/// stays focused on shaping data. Per-version licenses and the NuGet-registration
/// fields (description, deprecation) are enriched in later slices.
/// </remarks>
internal sealed class DepsDevPackageMetadataSource(
    HttpClient httpClient,
    ILogger<DepsDevPackageMetadataSource> logger)
    : IPackageMetadataSource
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<PackageMetadata?> GetAsync(PackageId id, CancellationToken cancellationToken)
    {
        var name = Uri.EscapeDataString(id.Value);

        var package = await GetJsonAsync<DepsDevPackageResponse>(
            $"v3/systems/nuget/packages/{name}",
            cancellationToken);

        var entries = package?.Versions?
            .Where(entry => !string.IsNullOrWhiteSpace(entry.VersionKey?.Version))
            .ToList();

        if (entries is null or { Count: 0 })
        {
            logger.LogInformation("deps.dev returned no versions for package {PackageId}.", id.Original);
            return null;
        }

        // The "default" version is what NuGet would install; fall back to the last
        // listed entry if the source did not flag one.
        var defaultEntry = entries.FirstOrDefault(entry => entry.IsDefault) ?? entries[^1];
        var detail = await GetVersionDetailAsync(name, defaultEntry.VersionKey!.Version!, cancellationToken);

        var versions = entries
            .Select(entry => new PackageVersionMetadata(
                entry.VersionKey!.Version!,
                entry.PublishedAt,
                entry.IsDeprecated,
                // Only the default version's license is fetched in Slice 1.
                entry.IsDefault ? FirstLicense(detail) : null))
            .ToList();

        return new PackageMetadata(
            PackageId: id.Value,
            Description: null,
            ProjectUrl: FindLink(detail, "HOMEPAGE"),
            SourceRepositoryUrl: FindLink(detail, "SOURCE_REPO"),
            License: FirstLicense(detail),
            IsDeprecated: detail?.IsDeprecated ?? false,
            LatestStableVersion: ComputeLatestStableVersion(versions),
            Versions: versions);
    }

    private Task<DepsDevVersionResponse?> GetVersionDetailAsync(string name, string version, CancellationToken cancellationToken) =>
        GetJsonAsync<DepsDevVersionResponse>(
            $"v3/systems/nuget/packages/{name}/versions/{Uri.EscapeDataString(version)}",
            cancellationToken);

    private async Task<T?> GetJsonAsync<T>(string relativeUrl, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(relativeUrl, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    /// <summary>Returns the highest stable version string, or <see langword="null"/> if none parse/are stable.</summary>
    private static string? ComputeLatestStableVersion(IEnumerable<PackageVersionMetadata> versions)
    {
        SemVer? latest = null;
        foreach (var version in versions)
        {
            if (SemVer.TryParse(version.Version, out var parsed) && parsed.IsStable && (latest is null || parsed > latest))
            {
                latest = parsed;
            }
        }

        return latest?.ToString();
    }

    /// <summary>Returns the first declared license id, or <see langword="null"/>.</summary>
    private static string? FirstLicense(DepsDevVersionResponse? detail) =>
        detail?.Licenses is { Count: > 0 } licenses ? licenses[0] : null;

    private static Uri? FindLink(DepsDevVersionResponse? detail, string label)
    {
        var url = detail?.Links?.FirstOrDefault(link =>
            string.Equals(link.Label, label, StringComparison.OrdinalIgnoreCase))?.Url;

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null;
    }
}
