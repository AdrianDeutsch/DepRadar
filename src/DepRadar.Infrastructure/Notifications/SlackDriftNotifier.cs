using System.Net.Http.Json;
using DepRadar.Application.History;
using DepRadar.Domain.History;
using DepRadar.Domain.ValueObjects;

namespace DepRadar.Infrastructure.Notifications;

/// <summary>
/// Posts a drift alert to a Slack-compatible incoming webhook (the client's base
/// address is the webhook URL). Best-effort: failures surface as a thrown exception
/// that the caller swallows, so a flaky webhook never fails a scan.
/// </summary>
internal sealed class SlackDriftNotifier(HttpClient httpClient) : IDriftNotifier
{
    /// <inheritdoc />
    public async Task NotifyAsync(DriftReport report, CancellationToken cancellationToken)
    {
        var payload = new { text = DriftAlertMessage.Format(report) };
        using var response = await httpClient.PostAsJsonAsync(string.Empty, payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    // Slack alerts are point-in-time messages; there is nothing to "close", so resolution is a no-op.
    public Task ResolveAsync(PackageId root, CancellationToken cancellationToken) => Task.CompletedTask;
}
