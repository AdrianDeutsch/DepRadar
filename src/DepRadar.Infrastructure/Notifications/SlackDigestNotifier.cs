using System.Net.Http.Json;
using DepRadar.Application.History;

namespace DepRadar.Infrastructure.Notifications;

/// <summary>
/// Posts a drift digest to a Slack-compatible incoming webhook (the client's base
/// address is the webhook URL).
/// </summary>
internal sealed class SlackDigestNotifier(HttpClient httpClient) : IDigestNotifier
{
    /// <inheritdoc />
    public async Task DeliverAsync(string markdown, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(string.Empty, new { text = markdown }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
