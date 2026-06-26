using DepRadar.Application.Abstractions;

namespace DepRadar.Infrastructure.External.GitHub;

/// <summary>No-op opener used when no GitHub token is configured.</summary>
internal sealed class NullPullRequestOpener : IPullRequestOpener
{
    /// <inheritdoc />
    public Task<string?> OpenAsync(PullRequestRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);
}
