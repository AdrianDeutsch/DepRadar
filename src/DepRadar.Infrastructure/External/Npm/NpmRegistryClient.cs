using System.Text.Json;
using DepRadar.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace DepRadar.Infrastructure.External.Npm;

/// <summary>
/// Reads package metadata from the npm registry (registry.npmjs.org). Responses are
/// cached so a resolution doesn't re-fetch the same package.
/// </summary>
internal sealed class NpmRegistryClient(HttpClient httpClient, HybridCache cache)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Fetches the registry document for <paramref name="name"/>, or null if unknown.</summary>
    public async Task<NpmPackageDocument?> GetAsync(string name, CancellationToken cancellationToken)
    {
        // Scoped packages (@scope/name) encode the slash in the path segment.
        var path = name.Replace("/", "%2F", StringComparison.Ordinal);

        var json = await cache.GetOrCreateAsync(
            $"npm:{name}",
            (httpClient, path),
            static async (state, token) =>
            {
                using var response = await state.httpClient.GetAsync(state.path, token);
                return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(token) : string.Empty;
            },
            HttpJsonCache.Options,
            cancellationToken: cancellationToken);

        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<NpmPackageDocument>(json, JsonOptions);
    }
}
