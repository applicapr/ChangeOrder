using System.Globalization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeOrder.Presentation.Extensions;

/// <summary>Composition entry-point for the Presentation layer.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Name of the fixed-window rate-limit policy applied to <c>/api/v1/change-orders</c>
    /// (research.md R-7).
    /// </summary>
    public const string ChangeOrdersRateLimitPolicy = "change-orders";

    /// <summary>
    /// Registers API versioning (Asp.Versioning), the fixed-window rate limiter
    /// (100 req/min/IP) and the built-in OpenAPI generator.
    /// </summary>
    public static IServiceCollection AddPresentationLayer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers["Retry-After"] = "60";
                return ValueTask.CompletedTask;
            };

            options.AddPolicy(ChangeOrdersRateLimitPolicy, httpContext =>
            {
                string partition = httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                    ?? "anonymous";
                return RateLimitPartition.GetFixedWindowLimiter(
                    partition,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    });
            });
        });

        services.AddOpenApi();
        return services;
    }

    /// <summary>Culture used by date formatting in this layer. Public to keep tests pinned.</summary>
    public static CultureInfo InvariantCulture => CultureInfo.InvariantCulture;
}
