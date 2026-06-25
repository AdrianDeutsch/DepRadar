using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Hybrid;

namespace DepRadar.Infrastructure.Caching;

/// <summary>
/// Caches external GET responses at the raw-JSON level so repeated/idempotent scans
/// don't burn API quota. Caching the response string (not parsed domain types) keeps
/// serialization trivial and provider-agnostic. A 404 is cached as an empty string so
/// "not found" is not re-fetched.
/// </summary>
internal static class HttpJsonCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Shared cache entry options (6-hour TTL) for all external responses.</summary>
    internal static readonly HybridCacheEntryOptions Options = new()
    {
        Expiration = TimeSpan.FromHours(6),
        LocalCacheExpiration = TimeSpan.FromHours(6),
    };

    /// <summary>Fetches and caches a GET response, deserializing it to <typeparamref name="T"/>.</summary>
    public static async Task<T?> GetAsync<T>(HybridCache cache, HttpClient httpClient, string key, string url, CancellationToken cancellationToken)
    {
        var json = await cache.GetOrCreateAsync(
            key,
            (httpClient, url),
            static (state, token) => FetchAsync(state.httpClient, state.url, token),
            Options,
            cancellationToken: cancellationToken);

        return string.IsNullOrEmpty(json) ? default : JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private static async ValueTask<string> FetchAsync(HttpClient httpClient, string url, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return string.Empty;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
