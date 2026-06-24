namespace DepRadar.Application.Messaging;

/// <summary>
/// Handles a single <typeparamref name="TRequest"/> and produces a
/// <typeparamref name="TResponse"/>. Exactly one handler is registered per request.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>Handles the request.</summary>
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
