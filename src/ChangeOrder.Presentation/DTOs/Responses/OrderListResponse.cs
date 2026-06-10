namespace ChangeOrder.Presentation.DTOs.Responses;

/// <summary>
/// Respuesta con la lista paginada de órdenes de cambio
/// </summary>

public sealed record OrderListResponse(
    IReadOnlyList<OrderResponse> Orders,
    int TotalCount
);
