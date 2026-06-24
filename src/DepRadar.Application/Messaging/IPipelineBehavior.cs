namespace DepRadar.Application.Messaging;

/// <summary>Continuation that invokes the next behavior or, finally, the handler.</summary>
/// <typeparam name="TResponse">The response type.</typeparam>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

/// <summary>
/// A cross-cutting behavior wrapped around request handling (logging, validation,
/// timing, …). Behaviors run in registration order, like an onion around the handler.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>Runs the behavior, calling <paramref name="next"/> to continue the pipeline.</summary>
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
