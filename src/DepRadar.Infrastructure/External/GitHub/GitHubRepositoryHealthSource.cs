using System.Text.Json.Serialization;
using DepRadar.Application.Abstractions;
using DepRadar.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Hybrid;

namespace DepRadar.Infrastructure.External.GitHub;

/// <summary>
/// <see cref="IRepositoryHealthSource"/> backed by the GitHub REST API. Reads the
/// <c>archived</c> flag and <c>pushed_at</c> for github.com repositories; other hosts
/// yield no signal. Works unauthenticated (60 req/h); a configured token raises the
/// limit. Responses are cached.
/// </summary>
internal sealed class GitHubRepositoryHealthSource(HttpClient httpClient, HybridCache cache) : IRepositoryHealthSource
{
    /// <inheritdoc />
    public async Task<RepositoryHealth?> GetAsync(Uri repositoryUrl, CancellationToken cancellationToken)
    {
        if (!TryGetRepository(repositoryUrl, out var owner, out var name))
        {
            return null;
        }

        var repository = await HttpJsonCache.GetAsync<GitHubRepository>(
            cache, httpClient, $"github:{owner}/{name}", $"repos/{owner}/{name}", cancellationToken);

        return repository is null ? null : new RepositoryHealth(repository.Archived, repository.PushedAt);
    }

    private static bool TryGetRepository(Uri url, out string owner, out string name)
    {
        owner = string.Empty;
        name = string.Empty;

        if (!url.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = url.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return false;
        }

        owner = segments[0];
        name = segments[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? segments[1][..^4] : segments[1];
        return owner.Length > 0 && name.Length > 0;
    }

    private sealed record GitHubRepository(
        bool Archived,
        [property: JsonPropertyName("pushed_at")] DateTimeOffset? PushedAt);
}
