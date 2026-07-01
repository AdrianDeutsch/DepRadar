using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

namespace DepRadar.Api;

/// <summary>
/// Optional edge hardening for the public API: an API-key gate (opt-in) and a
/// per-client fixed-window rate limiter. Both degrade to permissive defaults so the
/// service runs locally and in tests with zero configuration.
/// </summary>
/// <remarks>
/// Configuration (all optional):
/// <list type="bullet">
///   <item><c>Security:ApiKey</c> — when set, every <c>/api/*</c> request must carry a
///   matching <c>X-API-Key</c> header (constant-time compared). Unset ⇒ the API is open.</item>
///   <item><c>Security:RateLimitPerMinute</c> — permits per client per minute (default 300).</item>
/// </list>
/// Health probes, the dashboard and the SignalR hub stay open; only <c>/api/*</c> is gated.
/// </remarks>
internal static class ApiSecurity
{
    private const string ApiKeyHeader = "X-API-Key";

    /// <summary>Registers the per-client rate limiter (always on, generous default).</summary>
    public static IServiceCollection AddApiSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        var permitPerMinute = configuration.GetValue<int?>("Security:RateLimitPerMinute") ?? 300;

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ClientKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
        });

        return services;
    }

    /// <summary>Applies the rate limiter, then the API-key gate when a key is configured.</summary>
    public static WebApplication UseApiSecurity(this WebApplication app)
    {
        app.UseRateLimiter();

        var apiKey = app.Configuration["Security:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return app;
        }

        app.Use(async (context, next) =>
        {
            var open = !context.Request.Path.StartsWithSegments("/api");
            if (open || (context.Request.Headers.TryGetValue(ApiKeyHeader, out var provided) && KeyMatches(provided!, apiKey)))
            {
                await next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Missing or invalid API key.");
        });

        return app;
    }

    /// <summary>Partition key: the API key if present, else the remote IP.</summary>
    private static string ClientKey(HttpContext context) =>
        context.Request.Headers.TryGetValue(ApiKeyHeader, out var key) && !string.IsNullOrEmpty(key)
            ? key.ToString()
            : context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

    /// <summary>Constant-time key comparison over SHA-256 digests (avoids length/timing leaks).</summary>
    private static bool KeyMatches(string provided, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(provided)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
}
