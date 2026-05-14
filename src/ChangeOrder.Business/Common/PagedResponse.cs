namespace ChangeOrder.Business.Common;

/// <summary>
/// Generic paged result envelope returned by listing handlers and surfaced
/// 1:1 to the Presentation layer via <c>OrderMapper.ToResponse</c>.
/// </summary>
/// <typeparam name="T">Item type.</typeparam>
/// <param name="Items">Materialized page of items.</param>
/// <param name="TotalCount">Total number of items matching the query (across all pages).</param>
/// <param name="Page">Echoed 1-based page index.</param>
/// <param name="PageSize">Echoed page size.</param>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
