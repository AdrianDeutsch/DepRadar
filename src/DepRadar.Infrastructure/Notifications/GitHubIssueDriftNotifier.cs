using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DepRadar.Application.History;
using DepRadar.Domain.History;

namespace DepRadar.Infrastructure.Notifications;

/// <summary>
/// Reports drift as a GitHub issue, via the REST API (the client's base address is
/// api.github.com). De-duplicates: each root has one stable issue title, so a repeat
/// alert <em>comments</em> on the existing open issue instead of opening a new one.
/// Best-effort — a failure surfaces as a thrown exception the caller swallows.
/// </summary>
internal sealed class GitHubIssueDriftNotifier(HttpClient httpClient, GitHubAlertOptions options) : IDriftNotifier
{
    /// <inheritdoc />
    public async Task NotifyAsync(DriftReport report, CancellationToken cancellationToken)
    {
        var title = DriftIssue.Title(report);
        var body = DriftIssue.Body(report);

        var existing = await FindOpenIssueAsync(title, cancellationToken);
        if (existing is { } number)
        {
            using var comment = await httpClient.PostAsJsonAsync(
                $"repos/{options.Repository}/issues/{number}/comments", new { body }, cancellationToken);
            comment.EnsureSuccessStatusCode();
            return;
        }

        using var created = await httpClient.PostAsJsonAsync(
            $"repos/{options.Repository}/issues", new { title, body }, cancellationToken);
        created.EnsureSuccessStatusCode();
    }

    /// <summary>The number of an open issue with the exact title, or <see langword="null"/>.</summary>
    private async Task<int?> FindOpenIssueAsync(string title, CancellationToken cancellationToken)
    {
        var issues = await httpClient.GetFromJsonAsync<List<GitHubIssue>>(
            $"repos/{options.Repository}/issues?state=open&per_page=100", cancellationToken);

        // The issues endpoint also returns pull requests; exclude them.
        return issues?
            .FirstOrDefault(issue => issue.PullRequest is null && string.Equals(issue.Title, title, StringComparison.Ordinal))?
            .Number;
    }

    private sealed record GitHubIssue(
        int Number,
        string Title,
        [property: JsonPropertyName("pull_request")] object? PullRequest);
}
