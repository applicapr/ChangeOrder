using ChangeOrder.Business.Abstractions;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Errors;
using DomainChangeOrder = ChangeOrder.Domain.Entities.ChangeOrder;

namespace ChangeOrder.Business.Queries.GetOrderById;

/// <summary>
/// Handler for <see cref="GetOrderByIdQuery"/>. Returns the loaded aggregate
/// to the Presentation layer; <c>order.not_found</c> is surfaced when the
/// repository yields <c>null</c> (which, by virtue of the global query filter,
/// also covers the soft-deleted case — FR-009).
/// </summary>
public sealed class GetOrderByIdHandler : IQueryHandler<GetOrderByIdQuery, Result<DomainChangeOrder, Error>>
{
    private readonly IChangeOrderRepository _repository;

    /// <summary>Builds the handler with its single dependency.</summary>
    public GetOrderByIdHandler(IChangeOrderRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <inheritdoc/>
    public async Task<Result<DomainChangeOrder, Error>> HandleAsync(
        GetOrderByIdQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        DomainChangeOrder? order = await _repository
            .GetByIdAsNoTrackingAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        if (order is null)
        {
            return Result<DomainChangeOrder, Error>.Failure(DomainErrors.Order.NotFound(query.Id));
        }

        return Result<DomainChangeOrder, Error>.Success(order);
    }
}
