namespace ChangeOrder.Business.Queries.GetAllOrders;

/// <summary>
/// Query para obtener todas las órdenes de cambio de forma paginada
/// </summary>

public sealed record GetAllOrdersQuery(int Page, int PageSize);
