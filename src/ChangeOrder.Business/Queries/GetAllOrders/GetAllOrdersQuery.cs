namespace ChangeOrder.Business.Queries.GetAllOrders;

/// <summary>
/// CQRS query for <c>GET /api/v1/change-orders</c>. Carries the pagination
/// cursor; the handler validates the range (<see cref="Page"/> &gt;= 1,
/// <see cref="PageSize"/> in [1..50]) before hitting the repository.
/// </summary>
/// <param name="Page">1-based page index.</param>
/// <param name="PageSize">Page size; bounded by the constitution to [1..50].</param>
/// <param name="OrderNumber">
/// Optional prefix filter on <c>OrderNumber</c>. Accepts the full canonical
/// form (<c>20260513-02</c>) for exact lookup or just the date prefix
/// (<c>20260513</c>) for every order created that day. <c>null</c> disables
/// the filter. Validation tolerates digits and an optional dash separator.
/// </param>
public sealed record GetAllOrdersQuery(int Page, int PageSize, string? OrderNumber = null);
