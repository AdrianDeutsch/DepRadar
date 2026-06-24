using DepRadar.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace DepRadar.Api;

/// <summary>
/// Translates known application exceptions into RFC 9457 problem responses.
/// Unknown errors return a generic 500 without leaking internal details.
/// </summary>
internal sealed class GlobalExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            PackageNotFoundException => (StatusCodes.Status404NotFound, "Package not found"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
            FormatException => (StatusCodes.Status400BadRequest, "Invalid request"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred"),
        };

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Status = status,
                Title = title,
                // Surface the message only for client errors; never leak server-side detail.
                Detail = status == StatusCodes.Status500InternalServerError ? null : exception.Message,
            },
        });
    }
}
