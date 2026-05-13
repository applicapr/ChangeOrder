using System.Globalization;
using System.Text.RegularExpressions;
using ChangeOrder.Business.Abstractions;
using ChangeOrder.Business.Common;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Errors;
using DomainChangeOrder = ChangeOrder.Domain.Entities.ChangeOrder;
using DomainPagedRequest = ChangeOrder.Domain.Abstractions.PagedRequest;

namespace ChangeOrder.Business.Queries.GetAllOrders;

/// <summary>
/// Handler for <see cref="GetAllOrdersQuery"/>. Validates the pagination
/// cursor and returns a <see cref="PagedResponse{T}"/> of domain aggregates,
/// leaving the wire-shape projection to the Presentation mapper. Soft-deleted
/// rows are filtered out at the repository level (global query filter).
/// </summary>
public sealed partial class GetAllOrdersHandler
    : IQueryHandler<GetAllOrdersQuery, Result<PagedResponse<DomainChangeOrder>, Error>>
{
    private const int MinPage = 1;
    private const int MinPageSize = 1;
    private const int MaxPageSize = 50;
    private const int MaxOrderNumberFilterLength = 13;

    [GeneratedRegex(@"^\d{1,8}(-\d{1,2})?$", RegexOptions.CultureInvariant)]
    private static partial Regex OrderNumberFilterRegex();

    private readonly IChangeOrderRepository _repository;

    /// <summary>Builds the handler with its single dependency.</summary>
    public GetAllOrdersHandler(IChangeOrderRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <inheritdoc/>
    public async Task<Result<PagedResponse<DomainChangeOrder>, Error>> HandleAsync(
        GetAllOrdersQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        Result<TVoid, Error> validation = Validate(query);
        if (validation.IsFailure)
        {
            return Result<PagedResponse<DomainChangeOrder>, Error>.Failure(validation.Error!);
        }

        string? filter = NormalizeFilter(query.OrderNumber);
        DomainPagedRequest request = new(query.Page, query.PageSize, filter);

        IReadOnlyList<DomainChangeOrder> items = await _repository
            .ListAsync(request, cancellationToken)
            .ConfigureAwait(false);

        int totalCount = await _repository
            .CountAsync(filter, cancellationToken)
            .ConfigureAwait(false);

        PagedResponse<DomainChangeOrder> page = new(items, totalCount, query.Page, query.PageSize);
        return Result<PagedResponse<DomainChangeOrder>, Error>.Success(page);
    }

    private static string? NormalizeFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static Result<TVoid, Error> Validate(GetAllOrdersQuery query)
    {
        if (query.Page < MinPage)
        {
            return Result<TVoid, Error>.Failure(new Error(
                "validation.error",
                string.Format(CultureInfo.InvariantCulture, "Page must be >= {0}.", MinPage)));
        }

        if (query.PageSize < MinPageSize || query.PageSize > MaxPageSize)
        {
            return Result<TVoid, Error>.Failure(new Error(
                "validation.error",
                string.Format(
                    CultureInfo.InvariantCulture,
                    "PageSize must be between {0} and {1}.",
                    MinPageSize,
                    MaxPageSize)));
        }

        string? filter = NormalizeFilter(query.OrderNumber);
        if (filter is not null)
        {
            if (filter.Length > MaxOrderNumberFilterLength || !OrderNumberFilterRegex().IsMatch(filter))
            {
                return Result<TVoid, Error>.Failure(new Error(
                    "validation.error",
                    "OrderNumber filter must match yyyyMMdd or yyyyMMdd-## (digits and an optional dash separator)."));
            }
        }

        return Result<TVoid, Error>.Success(TVoid.Instance);
    }
}
