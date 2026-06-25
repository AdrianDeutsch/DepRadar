namespace DepRadar.Application.Abstractions;

/// <summary>
/// Port for fetching source-repository health (archived flag, last push) — implemented
/// in Infrastructure against the GitHub REST API.
/// </summary>
public interface IRepositoryHealthSource
{
    /// <summary>
    /// Returns repository health for a source URL, or <see langword="null"/> if the host
    /// is unsupported or the repository cannot be read.
    /// </summary>
    Task<RepositoryHealth?> GetAsync(Uri repositoryUrl, CancellationToken cancellationToken);
}

/// <summary>Repository health signals used for maintenance scoring.</summary>
public sealed record RepositoryHealth(bool Archived, DateTimeOffset? LastPushedAt);
