namespace ChangeOrder.Business.Queries.GetOrdersByDate;

/// <summary>
/// Query para obtener las órdenes de cambio de una fecha específica
/// </summary>

public sealed record GetOrdersByDateQuery(DateTime Date);
