namespace ChangeOrder.Business.Queries.GetOrderById;

/// <summary>
/// Query para obtener una orden de cambio por su Id.
/// </summary>

public sealed record GetOrderByIdQuery(Guid Id);
