namespace ChangeOrder.Business.Queries.GetAllOrders;

/// <summary>
/// CQRS query for <c>GET /api/v1/change-orders</c>. Carries the pagination
/// cursor; the handler validates the range (<see cref="Page"/> &gt;= 1,
/// <see cref="PageSize"/> in [1..50]) before hitting the repository.
/// </summary>
/// <param name="Page">1-based page index.</param>
/// <param name="PageSize">Page size; bounded by the constitution to [1..50].</param>
public sealed record GetAllOrdersQuery(int Page, int PageSize);
