using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace DepRadar.Application.Messaging;

/// <summary>
/// The hand-rolled mediator. Resolves the single handler for a request and wraps
/// it with the registered <see cref="IPipelineBehavior{TRequest,TResponse}"/> chain.
/// </summary>
/// <remarks>
/// Chosen over MediatR (commercial since July 2025) to keep the application core
/// free of a commercially-licensed dependency — the exact kind of license shift
/// DepRadar exists to flag. See <c>docs/adr/0002-handrolled-mediator.md</c>.
/// Request-type → wrapper resolution is cached so dispatch stays allocation-light.
/// </remarks>
public sealed class Mediator(IServiceProvider provider) : ISender
{
    private static readonly ConcurrentDictionary<Type, RequestHandlerWrapperBase> Wrappers = new();

    /// <inheritdoc />
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = (RequestHandlerWrapper<TResponse>)Wrappers.GetOrAdd(
            request.GetType(),
            static requestType =>
            {
                var wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, typeof(TResponse));
                return (RequestHandlerWrapperBase)Activator.CreateInstance(wrapperType)!;
            });

        return wrapper.Handle(request, provider, cancellationToken);
    }
}

/// <summary>Non-generic base so wrappers of any response type share one cache.</summary>
internal abstract class RequestHandlerWrapperBase;

/// <summary>Response-typed wrapper invoked by the mediator.</summary>
internal abstract class RequestHandlerWrapper<TResponse> : RequestHandlerWrapperBase
{
    public abstract Task<TResponse> Handle(object request, IServiceProvider provider, CancellationToken cancellationToken);
}

/// <summary>
/// Closed wrapper that knows the concrete request type, resolves the handler and
/// builds the behavior pipeline (innermost handler first, behaviors wrapped around).
/// </summary>
internal sealed class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    public override Task<TResponse> Handle(object request, IServiceProvider provider, CancellationToken cancellationToken)
    {
        var typedRequest = (TRequest)request;
        var handler = provider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();

        RequestHandlerDelegate<TResponse> pipeline = () => handler.Handle(typedRequest, cancellationToken);

        // Wrap in reverse so the first-registered behavior ends up outermost.
        foreach (var behavior in provider.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse())
        {
            var next = pipeline;
            pipeline = () => behavior.Handle(typedRequest, next, cancellationToken);
        }

        return pipeline();
    }
}
