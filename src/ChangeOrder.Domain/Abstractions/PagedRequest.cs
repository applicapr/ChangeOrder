namespace ChangeOrder.Domain.Abstractions;

/// <summary>
/// Pagination cursor shared between the repository (Domain) and the listing
/// handlers (Business). <see cref="Page"/> is 1-based; <see cref="PageSize"/>
/// is bounded to [1..50] by the constitution.
/// </summary>
/// <remarks>
/// Defined in Domain because <c>IChangeOrderRepository.ListAsync</c> needs the
/// shape and Domain MUST NOT depend on Business. Business consumes the same
/// type directly.
/// </remarks>
/// <param name="Page">1-based page index.</param>
/// <param name="PageSize">Page size, 1-50.</param>
/// <param name="OrderNumberFilter">
/// Optional prefix filter on <c>OrderNumber</c>. Accepts the full canonical
/// form (<c>20260513-02</c>) for exact lookup or just the date prefix
/// (<c>20260513</c>) to return every order created that day. <c>null</c>
/// disables the filter.
/// </param>
public sealed record PagedRequest(int Page, int PageSize, string? OrderNumberFilter = null);
