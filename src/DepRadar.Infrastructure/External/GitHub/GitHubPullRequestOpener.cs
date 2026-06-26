using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using DepRadar.Application.Abstractions;

namespace DepRadar.Infrastructure.External.GitHub;

/// <summary>
/// Opens a fix pull request through the GitHub REST API: read the base branch head,
/// create a branch, commit the patched file on it, and open the PR. The client's base
/// address is api.github.com and it carries the configured token.
/// </summary>
internal sealed class GitHubPullRequestOpener(HttpClient httpClient) : IPullRequestOpener
{
    /// <inheritdoc />
    public async Task<string?> OpenAsync(PullRequestRequest request, CancellationToken cancellationToken)
    {
        var repo = request.Repository;

        var baseRef = await httpClient.GetFromJsonAsync<GitRef>(
            $"repos/{repo}/git/ref/heads/{request.BaseBranch}", cancellationToken)
            ?? throw new InvalidOperationException($"Base branch '{request.BaseBranch}' was not found.");
        var baseSha = baseRef.Object?.Sha
            ?? throw new InvalidOperationException("The base branch ref carried no commit sha.");

        var file = await httpClient.GetFromJsonAsync<ContentFile>(
            $"repos/{repo}/contents/{request.FilePath}?ref={request.BaseBranch}", cancellationToken)
            ?? throw new InvalidOperationException($"File '{request.FilePath}' was not found on '{request.BaseBranch}'.");

        using (var createBranch = await httpClient.PostAsJsonAsync(
            $"repos/{repo}/git/refs",
            new { @ref = $"refs/heads/{request.HeadBranch}", sha = baseSha },
            cancellationToken))
        {
            createBranch.EnsureSuccessStatusCode();
        }

        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(request.NewContent));
        using (var commit = await httpClient.PutAsJsonAsync(
            $"repos/{repo}/contents/{request.FilePath}",
            new { message = request.Title, content = encoded, sha = file.Sha, branch = request.HeadBranch },
            cancellationToken))
        {
            commit.EnsureSuccessStatusCode();
        }

        using var pullRequest = await httpClient.PostAsJsonAsync(
            $"repos/{repo}/pulls",
            new { title = request.Title, head = request.HeadBranch, @base = request.BaseBranch, body = request.Body },
            cancellationToken);
        pullRequest.EnsureSuccessStatusCode();

        var created = await pullRequest.Content.ReadFromJsonAsync<CreatedPullRequest>(cancellationToken);
        return created?.HtmlUrl;
    }

    private sealed record GitRef(GitObject? Object);

    private sealed record GitObject(string? Sha);

    private sealed record ContentFile(string? Sha);

    private sealed record CreatedPullRequest([property: JsonPropertyName("html_url")] string? HtmlUrl);
}
