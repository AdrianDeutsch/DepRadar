using Microsoft.AspNetCore.SignalR;

namespace DepRadar.Api.Realtime;

/// <summary>
/// SignalR hub for live scan progress. Clients join the group for a specific scan id
/// and receive <c>ScanUpdated</c> messages as that scan transitions.
/// </summary>
internal sealed class ScanHub : Hub
{
    /// <summary>Joins the group for a scan to receive its updates.</summary>
    public Task Subscribe(string scanId) => Groups.AddToGroupAsync(Context.ConnectionId, scanId);

    /// <summary>Leaves a scan's group.</summary>
    public Task Unsubscribe(string scanId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, scanId);
}
