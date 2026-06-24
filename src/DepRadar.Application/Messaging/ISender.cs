namespace DepRadar.Application.Messaging;

/// <summary>
/// Dispatches a request to its single handler, running any registered pipeline
/// behaviors around it. This is the only mediator surface the rest of the
/// application depends on.
/// </summary>
public interface ISender
{
    /// <summary>Sends a request and returns its handler's response.</summary>
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
