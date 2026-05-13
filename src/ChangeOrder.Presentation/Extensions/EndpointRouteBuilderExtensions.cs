using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace ChangeOrder.Presentation.Extensions;

/// <summary>
/// Minimal-API endpoint mapping for the <c>/api/v1/change-orders</c> group.
/// Endpoint bodies are stubbed at this milestone; the actual handlers are
/// wired by the user-story phases (US1, US2, US3).
/// </summary>
public static class EndpointRouteBuilderExtensions
{
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

        group.MapPost("/", CreateChangeOrderStub)
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

    private static StatusCodeHttpResult CreateChangeOrderStub(HttpContext context) =>
        TypedResults.StatusCode(StatusCodes.Status501NotImplemented);

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
