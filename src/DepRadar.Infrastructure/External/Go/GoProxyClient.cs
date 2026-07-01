using System.Text;
using System.Text.Json;
using DepRadar.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace DepRadar.Infrastructure.External.Go;

/// <summary>
/// Reads module metadata from the Go module proxy (proxy.golang.org), cached. Keyless.
/// </summary>
/// <remarks>
/// The proxy speaks plain text/JSON: <c>@v/list</c> (one version per line),
/// <c>@latest</c> (JSON info), <c>@v/{version}.mod</c> (the go.mod file). Module paths
/// are case-encoded — every upper-case letter becomes <c>!</c> + lower-case
/// (<c>github.com/Azure</c> → <c>github.com/!azure</c>).
/// </remarks>
internal sealed class GoProxyClient(HttpClient httpClient, HybridCache cache)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>The tagged versions of a module (raw strings, e.g. <c>v1.2.3</c>); empty if none/unknown.</summary>
    public async Task<IReadOnlyList<string>> ListVersionsAsync(string module, CancellationToken cancellationToken)
    {
        var body = await GetAsync($"go-list:{module}", $"{Escape(module)}/@v/list", cancellationToken);
        return body is null
            ? []
            : body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>The proxy's latest known version (raw string), or null if the module is unknown.</summary>
    public async Task<string?> GetLatestVersionAsync(string module, CancellationToken cancellationToken)
    {
        var body = await GetAsync($"go-latest:{module}", $"{Escape(module)}/@latest", cancellationToken);
        if (body is null)
        {
            return null;
        }

        var info = JsonSerializer.Deserialize<LatestInfo>(body, JsonOptions);
        return info?.Version;
    }

    /// <summary>The go.mod file of one exact version (raw text), or null if unknown.</summary>
    public Task<string?> GetModFileAsync(string module, string rawVersion, CancellationToken cancellationToken) =>
        GetAsync($"go-mod:{module}:{rawVersion}", $"{Escape(module)}/@v/{rawVersion}.mod", cancellationToken);

    /// <summary>Case-encodes a module path per the proxy protocol.</summary>
    internal static string Escape(string module)
    {
        var builder = new StringBuilder(module.Length);
        foreach (var character in module)
        {
            if (char.IsUpper(character))
            {
                builder.Append('!').Append(char.ToLowerInvariant(character));
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private async Task<string?> GetAsync(string cacheKey, string path, CancellationToken cancellationToken)
    {
        var body = await cache.GetOrCreateAsync(
            cacheKey,
            (httpClient, path),
            static async (state, token) =>
            {
                using var response = await state.httpClient.GetAsync(state.path, token);
                return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(token) : string.Empty;
            },
            HttpJsonCache.Options,
            cancellationToken: cancellationToken);

        return string.IsNullOrEmpty(body) ? null : body;
    }

    private sealed record LatestInfo(string? Version);
}
