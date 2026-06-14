using ChangeOrder.Business.Abstractions;
using ChangeOrder.Business.Commands.CreateOrder;
using ChangeOrder.Business.Commands.DeleteOrder;
using ChangeOrder.Business.Commands.UpdateOrder;
using ChangeOrder.Business.Queries.GetAllOrders;
using ChangeOrder.Business.Queries.GetOrderById;
using ChangeOrder.Business.Queries.GetOrdersByDate;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Errors;
using ChangeOrder.Presentation.DTOs.Requests;
using ChangeOrder.Presentation.Mappers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http; 
using Microsoft.AspNetCore.Routing;
using ChangeOrder.Domain.Enums;

namespace ChangeOrder.Presentation.Endpoints;

/// <summary>
/// Endpoints Minimal API para la gestión de órdenes de cambio
/// </summary>


public static class ChangeOrderEndpoints
{
    /// <summary>
    /// Registra todos los endpoints de órdenes de cambio bajo /api/v1/change-orders
    /// </summary>

    public static IEndpointRouteBuilder MapChangeOrderEndpoints(
    this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/change-orders")
            .WithTags("ChangeOrders");

        // GET por Id
        group.MapGet("{id:guid}", async (
            Guid id,
            IQueryHandler<GetOrderByIdQuery, ChangeOrderEntity> handler,
            CancellationToken ct) =>
        {
            Result<ChangeOrderEntity, Error> result =
                await handler.HandleAsync(new GetOrderByIdQuery(id), ct);

            if (!result.IsSuccess)
                return Results.NotFound(result.Error);

            return Results.Ok(OrderMapper.ToResponse(result.Value!));
        }).WithName("GetOrderById");

        // GET todos
        group.MapGet("", async (
            IQueryHandler<GetAllOrdersQuery, IReadOnlyList<ChangeOrderEntity>> handler,
            CancellationToken ct,
            int page = 1,
            int pageSize = 10) =>
        {
            Result<IReadOnlyList<ChangeOrderEntity>, Error> result =
                await handler.HandleAsync(new GetAllOrdersQuery(page, pageSize), ct);

            return Results.Ok(OrderMapper.ToResponseList(result.Value!));
        }).WithName("GetAllOrders");

        // GET por fecha
        group.MapGet("date/{date}", async (
            DateTime date,
            IQueryHandler<GetOrdersByDateQuery, IReadOnlyList<ChangeOrderEntity>> handler,
            CancellationToken ct) =>
        {
            Result<IReadOnlyList<ChangeOrderEntity>, Error> result =
                await handler.HandleAsync(new GetOrdersByDateQuery(date), ct);

            return Results.Ok(OrderMapper.ToResponseList(result.Value!));
        }).WithName("GetOrdersByDate");

        // POST crear
        group.MapPost("", async (
            CreateOrderRequest request,
            ICommandHandler<CreateOrderCommand, Guid> handler,
            CancellationToken ct) =>
        {
            CreateOrderCommand command = new(
                request.IdempotencyKey,
                request.ProgramName,
                request.ProductionVersion,
                request.VersionScreenshotPath,
                request.RequestDate,
                request.WorkDescription,
                request.RequestDetails,
                request.Justification,
                request.RequiredAction,
                request.RequesterName,
                request.RequesterPosition,
                request.RequesterDepartment,
                request.RequesterEmail);

            Result<Guid, Error> result = await handler.HandleAsync(command, ct);

            if (!result.IsSuccess)
                return Results.BadRequest(result.Error);

            return Results.Created($"/api/v1/change-orders/{result.Value}", result.Value);
        }).WithName("CreateOrder");

        // PUT actualizar
        group.MapPut("{id:guid}", async (
            Guid id,
            UpdateOrderRequest request,
            ICommandHandler<UpdateOrderCommand, Guid> handler,
            CancellationToken ct) =>
        {
            UpdateOrderCommand command = new(
                id,
                request.ProgramName,
                request.ProductionVersion,
                request.VersionScreenshotPath,
                request.WorkDescription,
                request.RequestDetails,
                request.Justification,
                request.RequiredAction,
                request.DeliveryDate,
                Enum.Parse<OrderStatus>(request.Status));

            Result<Guid, Error> result = await handler.HandleAsync(command, ct);

            if (!result.IsSuccess)
                return Results.NotFound(result.Error);

            return Results.NoContent();
        }).WithName("UpdateOrder");

        // DELETE
        group.MapDelete("{id:guid}", async (
            Guid id,
            ICommandHandler<DeleteOrderCommand, Guid> handler,
            CancellationToken ct) =>
        {
            Result<Guid, Error> result =
                await handler.HandleAsync(new DeleteOrderCommand(id), ct);

            if (!result.IsSuccess)
                return Results.NotFound(result.Error);

            return Results.NoContent();
        }).WithName("DeleteOrder");

        return app;
    }
}
