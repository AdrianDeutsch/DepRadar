namespace DepRadar.Application.Abstractions;

/// <summary>
/// Port for opening a pull request that applies a fix. Implemented in Infrastructure
/// against the GitHub REST API; a no-op implementation is used when no token is set.
/// </summary>
public interface IPullRequestOpener
{
    /// <summary>
    /// Commits <paramref name="request"/>'s new file content on a fresh branch and opens a
    /// PR. Returns the PR URL, or <see langword="null"/> when no opener is configured.
    /// </summary>
    Task<string?> OpenAsync(PullRequestRequest request, CancellationToken cancellationToken);
}

/// <summary>Everything needed to raise a one-file fix PR.</summary>
/// <param name="Repository">The target repository as <c>owner/name</c>.</param>
/// <param name="BaseBranch">The branch to target (e.g. <c>main</c>).</param>
/// <param name="HeadBranch">The new branch to create for the change.</param>
/// <param name="FilePath">Path of the file to update, relative to the repo root.</param>
/// <param name="NewContent">The patched file content.</param>
/// <param name="Title">The PR title.</param>
/// <param name="Body">The PR body (Markdown).</param>
public sealed record PullRequestRequest(
    string Repository,
    string BaseBranch,
    string HeadBranch,
    string FilePath,
    string NewContent,
    string Title,
    string Body);
