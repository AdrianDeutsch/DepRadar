namespace DepRadar.Application.History;

/// <summary>
/// Port for delivering a ready-rendered Markdown drift digest to an outside channel
/// (e.g. a Slack webhook). Best-effort and never throws into the caller's hot path.
/// </summary>
public interface IDigestNotifier
{
    /// <summary>Delivers the digest <paramref name="markdown"/>.</summary>
    Task DeliverAsync(string markdown, CancellationToken cancellationToken);
}
