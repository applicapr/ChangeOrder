namespace ChangeOrder.Business.Common;

/// <summary>
/// Business-layer pagination cursor used by listing handlers. Mirrors and
/// translates to <see cref="ChangeOrder.Domain.Abstractions.PagedRequest"/>
/// at the repository boundary; kept as a separate type so the Business
/// layer can later add filtering/sorting without leaking changes into Domain.
/// </summary>
/// <param name="Page">1-based page index.</param>
/// <param name="PageSize">Page size, 1-50.</param>
public sealed record PagedRequest(int Page, int PageSize);
