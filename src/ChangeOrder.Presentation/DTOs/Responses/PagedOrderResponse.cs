namespace ChangeOrder.Presentation.DTOs.Responses;

/// <summary>
/// Wire shape returned by <c>GET /api/v1/change-orders</c>. Matches the
/// <c>PagedOrderResponse</c> schema in <c>contracts/openapi.yaml</c>.
/// </summary>
/// <param name="Items">Materialized page of orders.</param>
/// <param name="TotalCount">Total count of non-soft-deleted orders matching the query.</param>
/// <param name="Page">Echoed 1-based page index.</param>
/// <param name="PageSize">Echoed page size.</param>
public sealed record PagedOrderResponse(
    IReadOnlyList<OrderResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
