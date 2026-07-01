using System.Text.Json;
using DepRadar.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace DepRadar.Infrastructure.External.Cargo;

/// <summary>Reads crate metadata from the crates.io API, cached per crate/version.</summary>
internal sealed class CargoRegistryClient(HttpClient httpClient, HybridCache cache)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Fetches a crate's version list, or null if unknown.</summary>
    public async Task<CargoCrateDocument?> GetAsync(string crate, CancellationToken cancellationToken)
    {
        var json = await GetJsonAsync($"cargo:{crate}", $"api/v1/crates/{crate}", cancellationToken);
        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<CargoCrateDocument>(json, JsonOptions);
    }

    /// <summary>Fetches one version's declared dependencies, or null if unknown.</summary>
    public async Task<CargoDependenciesDocument?> GetDependenciesAsync(string crate, string version, CancellationToken cancellationToken)
    {
        var json = await GetJsonAsync($"cargo-deps:{crate}:{version}", $"api/v1/crates/{crate}/{version}/dependencies", cancellationToken);
        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<CargoDependenciesDocument>(json, JsonOptions);
    }

    private async Task<string> GetJsonAsync(string cacheKey, string path, CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(
            cacheKey,
            (httpClient, path),
            static async (state, token) =>
            {
                using var response = await state.httpClient.GetAsync(state.path, token);
                return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(token) : string.Empty;
            },
            HttpJsonCache.Options,
            cancellationToken: cancellationToken);
}
