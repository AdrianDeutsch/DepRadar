using System.Diagnostics;
using DepRadar.Application.Messaging;
using Microsoft.Extensions.Logging;

namespace DepRadar.Application.Behaviors;

/// <summary>
/// Pipeline behavior that logs every request, its outcome and how long it took.
/// Demonstrates why the mediator exists: cross-cutting concerns live in one place
/// instead of being copy-pasted into every handler.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <inheritdoc />
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var startedAt = Stopwatch.GetTimestamp();

        logger.LogInformation("Handling {RequestName}", requestName);
        try
        {
            var response = await next();
            logger.LogInformation(
                "Handled {RequestName} in {ElapsedMs:F1} ms",
                requestName,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return response;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "{RequestName} failed", requestName);
            throw;
        }
    }
}
