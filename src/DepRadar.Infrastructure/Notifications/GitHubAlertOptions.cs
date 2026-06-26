namespace DepRadar.Infrastructure.Notifications;

/// <summary>Configuration for the GitHub-issue drift channel.</summary>
/// <param name="Repository">The target repository as <c>owner/name</c>.</param>
internal sealed record GitHubAlertOptions(string Repository);
