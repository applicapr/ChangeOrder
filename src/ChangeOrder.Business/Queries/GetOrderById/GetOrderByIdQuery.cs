namespace ChangeOrder.Business.Queries.GetOrderById;

/// <summary>
/// CQRS query for <c>GET /api/v1/change-orders/{id}</c>. The handler returns
/// the materialized aggregate (without ever materializing a soft-deleted row,
/// thanks to the global query filter installed in
/// <c>ChangeOrderConfiguration</c>).
/// </summary>
/// <param name="Id">Aggregate identifier requested by the caller.</param>
public sealed record GetOrderByIdQuery(Guid Id);
