using System.Reflection;
using Asp.Versioning;
using Asp.Versioning.Builder;
using ChangeOrder.Business.Abstractions;
using ChangeOrder.Business.Commands.CreateOrder;
using ChangeOrder.Domain.Errors;
using ChangeOrder.Presentation.Common;
using ChangeOrder.Presentation.DTOs.Requests;
using ChangeOrder.Presentation.DTOs.Responses;
using ChangeOrder.Presentation.Mappers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace ChangeOrder.Presentation.Extensions;

/// <summary>
/// Minimal-API endpoint mapping for the <c>/api/v1/change-orders</c> group.
/// US1 (Create) is implemented; the rest of the verbs remain stubbed at
/// 501 Not Implemented until their user stories are tackled.
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    /// <summary>HTTP header name carrying the client-supplied idempotency key.</summary>
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    private const int IdempotencyKeyMinLength = 8;
    private const int IdempotencyKeyMaxLength = 64;

    private const string ServiceName = "ChangeOrder.Api";

    /// <summary>
    /// Maps the global <c>GET /version</c> endpoint. Lives outside the
    /// versioned <c>/api/v{version}</c> group so monitoring tools can probe
    /// the build identity without negotiating an API version.
    /// </summary>
    public static IEndpointRouteBuilder MapVersionEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/version", (IHostEnvironment env) =>
        {
            string informational = Assembly.GetEntryAssembly()?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? "unknown";
            int plusIndex = informational.IndexOf('+', StringComparison.Ordinal);
            string version = plusIndex < 0 ? informational : informational[..plusIndex];

            return TypedResults.Ok(new VersionResponse(
                Name: ServiceName,
                Version: version,
                Environment: env.EnvironmentName));
        })
        .WithName("getVersion")
        .WithTags("meta");

        return endpoints;
    }

    /// <summary>Maps the change-order routes onto <paramref name="endpoints"/>.</summary>
    public static IEndpointRouteBuilder MapChangeOrderApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        ApiVersionSet apiVersionSet = endpoints.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1, 0))
            .ReportApiVersions()
            .Build();

        RouteGroupBuilder group = endpoints
            .MapGroup("/api/v{version:apiVersion}/change-orders")
            .WithApiVersionSet(apiVersionSet)
            .RequireRateLimiting(ServiceCollectionExtensions.ChangeOrdersRateLimitPolicy)
            .WithTags("change-orders");

        group.MapPost("/", CreateChangeOrderAsync)
            .WithName("createChangeOrder");
        group.MapGet("/", ListChangeOrdersStub)
            .WithName("listChangeOrders");
        group.MapGet("/{id:guid}", GetChangeOrderByIdStub)
            .WithName("getChangeOrderById");
        group.MapPut("/{id:guid}", UpdateChangeOrderStub)
            .WithName("updateChangeOrder");
        group.MapDelete("/{id:guid}", DeleteChangeOrderStub)
            .WithName("deleteChangeOrder");
        group.MapPut("/{id:guid}/approvals/{level}", RecordApprovalStub)
            .WithName("recordApproval");
        group.MapPatch("/{id:guid}/dates", RecordMilestoneDatesStub)
            .WithName("recordMilestoneDates");

        return endpoints;
    }

    private static async Task<IResult> CreateChangeOrderAsync(
        CreateOrderRequest request,
        HttpContext httpContext,
        ICommandHandler<CreateOrderCommand, Result<CreateOrderResult, Error>> handler,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            ProblemDetails missingBody = new()
            {
                Type = "https://changeorder.internal.example/errors/validation.error",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Request body is required."
            };
            missingBody.Extensions["code"] = "validation.error";
            return TypedResults.Problem(missingBody);
        }

        if (!httpContext.Request.Headers.TryGetValue(IdempotencyKeyHeader, out Microsoft.Extensions.Primitives.StringValues keyValues))
        {
            return BadRequest("idempotency.key_missing", "Idempotency-Key header is required.");
        }

        string? idempotencyKey = keyValues.ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest("idempotency.key_missing", "Idempotency-Key header is required.");
        }

        if (idempotencyKey.Length is < IdempotencyKeyMinLength or > IdempotencyKeyMaxLength)
        {
            return BadRequest(
                "idempotency.key_invalid",
                $"Idempotency-Key length must be between {IdempotencyKeyMinLength} and {IdempotencyKeyMaxLength} characters.");
        }

        CreateOrderCommand command = request.ToCommand(idempotencyKey);
        Result<CreateOrderResult, Error> result = await handler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            ProblemDetails problem = ProblemDetailsFactory.FromError(result.Error!, httpContext.Request.Path);
            return TypedResults.Problem(problem);
        }

        CreateOrderResult success = result.Value!;
        OrderResponse response = success.Order.ToResponse();

        if (success.WasReplay)
        {
            return TypedResults.Ok(response);
        }

        string location = $"/api/v1/change-orders/{response.Id}";
        return TypedResults.Created(location, response);
    }

    private static ProblemHttpResult BadRequest(string code, string detail)
    {
        ProblemDetails problem = new()
        {
            Type = $"https://changeorder.internal.example/errors/{code}",
            Title = "Bad Request",
            Status = StatusCodes.Status400BadRequest,
            Detail = detail
        };
        problem.Extensions["code"] = code;
        return TypedResults.Problem(problem);
    }

    private static StatusCodeHttpResult ListChangeOrdersStub(HttpContext context) =>
        TypedResults.StatusCode(StatusCodes.Status501NotImplemented);

    private static StatusCodeHttpResult GetChangeOrderByIdStub(Guid id) =>
        TypedResults.StatusCode(StatusCodes.Status501NotImplemented);

    private static StatusCodeHttpResult UpdateChangeOrderStub(Guid id) =>
        TypedResults.StatusCode(StatusCodes.Status501NotImplemented);

    private static StatusCodeHttpResult DeleteChangeOrderStub(Guid id) =>
        TypedResults.StatusCode(StatusCodes.Status501NotImplemented);

    private static StatusCodeHttpResult RecordApprovalStub(Guid id, string level) =>
        TypedResults.StatusCode(StatusCodes.Status501NotImplemented);

    private static StatusCodeHttpResult RecordMilestoneDatesStub(Guid id) =>
        TypedResults.StatusCode(StatusCodes.Status501NotImplemented);
}
