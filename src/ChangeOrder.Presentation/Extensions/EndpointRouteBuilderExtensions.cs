using System.Reflection;
using Asp.Versioning;
using Asp.Versioning.Builder;
using ChangeOrder.Business.Abstractions;
using ChangeOrder.Business.Commands.CreateOrder;
using ChangeOrder.Business.Commands.DeleteOrder;
using ChangeOrder.Business.Commands.RecordApproval;
using ChangeOrder.Business.Commands.RecordMilestoneDates;
using ChangeOrder.Business.Commands.UpdateOrder;
using ChangeOrder.Business.Common;
using ChangeOrder.Business.Queries.GetAllOrders;
using ChangeOrder.Business.Queries.GetOrderById;
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
using DomainChangeOrder = ChangeOrder.Domain.Entities.ChangeOrder;

namespace ChangeOrder.Presentation.Extensions;

/// <summary>
/// Minimal-API endpoint mapping for the <c>/api/v1/change-orders</c> group.
/// US1 (Create), US2 (Approval workflow + milestone dates) and US3
/// (list / get / update / delete) are fully wired here.
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
        .WithTags("meta")
        .WithSummary("Service identity and version")
        .WithDescription(
            "Returns the running service name, semantic version (sourced from " +
            "AssemblyInformationalVersionAttribute) and hosting environment. " +
            "Lives outside `/api/v{version}` so monitoring tools can probe " +
            "build identity without negotiating an API version.")
        .Produces<VersionResponse>(StatusCodes.Status200OK);

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
            .WithName("createChangeOrder")
            .WithSummary("Create a new change order (idempotent via Idempotency-Key)")
            .WithDescription(
                "Creates a new ChangeOrder, assigns a thread-safe OrderNumber in " +
                "format `yyyyMMdd-##` and honors the `Idempotency-Key` request header. " +
                "Replaying the same key with the same payload returns 200 with the " +
                "original row; same key with a different payload returns 422 " +
                "(`idempotency.payload_divergence`).")
            .Produces<OrderResponse>(StatusCodes.Status201Created)
            .Produces<OrderResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/", ListChangeOrdersAsync)
            .WithName("listChangeOrders")
            .WithSummary("List change orders (paginated, excludes soft-deleted)")
            .WithDescription(
                "Returns a paginated page of non-soft-deleted change orders, ordered by " +
                "`RequestDate` descending. Page defaults to 1, pageSize defaults to 10, " +
                "and pageSize is bounded to [1..50] by the constitution. Soft-deleted " +
                "rows are filtered out via the EF Core global query filter (FR-009).")
            .Produces<PagedOrderResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/{id:guid}", GetChangeOrderByIdAsync)
            .WithName("getChangeOrderById")
            .WithSummary("Fetch a single change order by id")
            .WithDescription(
                "Returns the canonical `OrderResponse`, including the base64-encoded " +
                "`rowVersion` the client must echo on subsequent `PUT` requests for " +
                "optimistic-concurrency control (FR-013). Soft-deleted rows are not " +
                "visible — calling this endpoint after `DELETE` returns 404.")
            .Produces<OrderResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPut("/{id:guid}", UpdateChangeOrderAsync)
            .WithName("updateChangeOrder")
            .WithSummary("Update a change order (only in Draft state, optimistic concurrency)")
            .WithDescription(
                "Replaces the editable content of the order. Allowed only while in " +
                "`Draft` (FR-006 / C-1); any other state returns 409 with " +
                "`order.edit_after_draft`. The submitted `rowVersion` must match the " +
                "persisted SQL Server `rowversion` token — a mismatch returns 409 with " +
                "`order.concurrency_conflict` (FR-013).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapDelete("/{id:guid}", DeleteChangeOrderAsync)
            .WithName("deleteChangeOrder")
            .WithSummary("Soft-delete a change order")
            .WithDescription(
                "Soft-deletes the order: the row is preserved, `IsDeleted` is set to " +
                "`true` and `DeletedAt` to the UTC timestamp (FR-008/009). The " +
                "`AuditInterceptor` performs the conversion on `SaveChangesAsync`. " +
                "Subsequent `GET`/`LIST` calls will not return the row.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPut("/{id:guid}/approvals/{level}", RecordApprovalAsync)
            .WithName("recordApproval")
            .WithSummary("Record an approval verdict for one of the four chain levels")
            .WithDescription(
                "Records a verdict (`Pending` | `Approved` | `Rejected`) on one of the four " +
                "approval slots: `requester`, `departmentHead`, `itHead`, `programmingDivision`. " +
                "When all four are `Approved` the order advances `PendingApproval → Approved`. " +
                "Returns 204 on success, 404 when the order does not exist, 409 on illegal " +
                "transitions, and 400 on validation errors (unknown level / verdict).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPatch("/{id:guid}/dates", RecordMilestoneDatesAsync)
            .WithName("recordMilestoneDates")
            .WithSummary("Record milestone dates (delivery, evaluation, production deploy)")
            .WithDescription(
                "Records one or more milestone dates. Setting `deliveryDate` while in " +
                "`Approved` advances the order to `InProgress`; setting `productionDeployDate` " +
                "while in `InProgress` advances it to `Deployed`. Out-of-order updates return " +
                "409 (`order.invalid_transition`).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

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

    private static async Task<IResult> RecordApprovalAsync(
        Guid id,
        string level,
        ApprovalVerdictRequest request,
        HttpContext httpContext,
        ICommandHandler<RecordApprovalCommand, Result<TVoid, Error>> handler,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("validation.error", "Request body is required.");
        }

        Result<RecordApprovalCommand, Error> mapped = request.ToCommand(id, level);
        if (mapped.IsFailure)
        {
            ProblemDetails validationProblem = ProblemDetailsFactory.FromError(mapped.Error!, httpContext.Request.Path);
            return TypedResults.Problem(validationProblem);
        }

        Result<TVoid, Error> result = await handler.HandleAsync(mapped.Value!, cancellationToken);
        if (result.IsFailure)
        {
            ProblemDetails problem = ProblemDetailsFactory.FromError(result.Error!, httpContext.Request.Path);
            return TypedResults.Problem(problem);
        }

        return TypedResults.NoContent();
    }

    private static async Task<IResult> RecordMilestoneDatesAsync(
        Guid id,
        MilestoneDatesRequest request,
        HttpContext httpContext,
        ICommandHandler<RecordMilestoneDatesCommand, Result<TVoid, Error>> handler,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("validation.error", "Request body is required.");
        }

        RecordMilestoneDatesCommand command = request.ToCommand(id);
        Result<TVoid, Error> result = await handler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            ProblemDetails problem = ProblemDetailsFactory.FromError(result.Error!, httpContext.Request.Path);
            return TypedResults.Problem(problem);
        }

        return TypedResults.NoContent();
    }

    private const int DefaultPage = 1;
    private const int DefaultPageSize = 10;

    private static async Task<IResult> ListChangeOrdersAsync(
        HttpContext httpContext,
        IQueryHandler<GetAllOrdersQuery, Result<PagedResponse<DomainChangeOrder>, Error>> handler,
        CancellationToken cancellationToken,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null)
    {
        GetAllOrdersQuery query = new(
            Page: page ?? DefaultPage,
            PageSize: pageSize ?? DefaultPageSize);

        Result<PagedResponse<DomainChangeOrder>, Error> result = await handler.HandleAsync(query, cancellationToken);
        if (result.IsFailure)
        {
            ProblemDetails problem = ProblemDetailsFactory.FromError(result.Error!, httpContext.Request.Path);
            return TypedResults.Problem(problem);
        }

        PagedOrderResponse response = result.Value!.ToResponse();
        return TypedResults.Ok(response);
    }

    private static async Task<IResult> GetChangeOrderByIdAsync(
        Guid id,
        HttpContext httpContext,
        IQueryHandler<GetOrderByIdQuery, Result<DomainChangeOrder, Error>> handler,
        CancellationToken cancellationToken)
    {
        GetOrderByIdQuery query = new(id);
        Result<DomainChangeOrder, Error> result = await handler.HandleAsync(query, cancellationToken);

        if (result.IsFailure)
        {
            ProblemDetails problem = ProblemDetailsFactory.FromError(result.Error!, httpContext.Request.Path);
            return TypedResults.Problem(problem);
        }

        return TypedResults.Ok(result.Value!.ToResponse());
    }

    private static async Task<IResult> UpdateChangeOrderAsync(
        Guid id,
        UpdateOrderRequest request,
        HttpContext httpContext,
        ICommandHandler<UpdateOrderCommand, Result<TVoid, Error>> handler,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("validation.error", "Request body is required.");
        }

        Result<UpdateOrderCommand, Error> mapped = request.ToCommand(id);
        if (mapped.IsFailure)
        {
            ProblemDetails validationProblem = ProblemDetailsFactory.FromError(mapped.Error!, httpContext.Request.Path);
            return TypedResults.Problem(validationProblem);
        }

        Result<TVoid, Error> result = await handler.HandleAsync(mapped.Value!, cancellationToken);
        if (result.IsFailure)
        {
            ProblemDetails problem = ProblemDetailsFactory.FromError(result.Error!, httpContext.Request.Path);
            return TypedResults.Problem(problem);
        }

        return TypedResults.NoContent();
    }

    private static async Task<IResult> DeleteChangeOrderAsync(
        Guid id,
        HttpContext httpContext,
        ICommandHandler<DeleteOrderCommand, Result<TVoid, Error>> handler,
        CancellationToken cancellationToken)
    {
        DeleteOrderCommand command = new(id);
        Result<TVoid, Error> result = await handler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            ProblemDetails problem = ProblemDetailsFactory.FromError(result.Error!, httpContext.Request.Path);
            return TypedResults.Problem(problem);
        }

        return TypedResults.NoContent();
    }
}
