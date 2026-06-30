using System.Text.Json;
using DepRadar.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace DepRadar.Infrastructure.External.PyPi;

/// <summary>Reads package metadata from the PyPI JSON API, cached per (name, version).</summary>
internal sealed class PyPiRegistryClient(HttpClient httpClient, HybridCache cache)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Fetches the document for a package (latest when <paramref name="version"/> is null,
    /// otherwise that exact version), or null if unknown.
    /// </summary>
    public async Task<PyPiDocument?> GetAsync(string name, string? version, CancellationToken cancellationToken)
    {
        var path = version is null ? $"pypi/{name}/json" : $"pypi/{name}/{version}/json";

        var json = await cache.GetOrCreateAsync(
            $"pypi:{name}:{version ?? "latest"}",
            (httpClient, path),
            static async (state, token) =>
            {
                using var response = await state.httpClient.GetAsync(state.path, token);
                return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(token) : string.Empty;
            },
            HttpJsonCache.Options,
            cancellationToken: cancellationToken);

        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<PyPiDocument>(json, JsonOptions);
    }
}
