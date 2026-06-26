using System.Net.Http.Json;
using DepRadar.Application.History;
using DepRadar.Domain.History;

namespace DepRadar.Infrastructure.Notifications;

/// <summary>
/// Opens a GitHub issue describing the drift, via the REST API (the client's base
/// address is api.github.com). An alternative to Slack for teams that triage in their
/// issue tracker. Best-effort: a failure surfaces as a thrown exception the caller swallows.
/// </summary>
internal sealed class GitHubIssueDriftNotifier(HttpClient httpClient, GitHubAlertOptions options) : IDriftNotifier
{
    /// <inheritdoc />
    public async Task NotifyAsync(DriftReport report, CancellationToken cancellationToken)
    {
        var payload = new { title = DriftIssue.Title(report), body = DriftIssue.Body(report) };
        using var response = await httpClient.PostAsJsonAsync($"repos/{options.Repository}/issues", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
