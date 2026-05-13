using ChangeOrder.Business.Abstractions;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Errors;
using Microsoft.Extensions.Logging;
using DomainChangeOrder = ChangeOrder.Domain.Entities.ChangeOrder;

namespace ChangeOrder.Business.Commands.DeleteOrder;

/// <summary>
/// Handler for <see cref="DeleteOrderCommand"/>. Loads the aggregate; if it is
/// missing (or already soft-deleted, which the global query filter makes
/// indistinguishable from missing), returns <c>order.not_found</c>.
/// Otherwise calls <c>IChangeOrderRepository.Remove</c> — the
/// <c>AuditInterceptor</c> then converts the <c>EntityState.Deleted</c> entry
/// into a soft delete on <c>SaveChangesAsync</c> (FR-008/009).
/// </summary>
public sealed partial class DeleteOrderHandler : ICommandHandler<DeleteOrderCommand, Result<TVoid, Error>>
{
    private readonly IChangeOrderRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteOrderHandler> _logger;

    /// <summary>Builds the handler with its three dependencies.</summary>
    public DeleteOrderHandler(
        IChangeOrderRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<DeleteOrderHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);

        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Result<TVoid, Error>> HandleAsync(
        DeleteOrderCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        DomainChangeOrder? order = await _repository
            .GetByIdAsync(command.OrderId, cancellationToken)
            .ConfigureAwait(false);

        if (order is null)
        {
            return Result<TVoid, Error>.Failure(DomainErrors.Order.NotFound(command.OrderId));
        }

        _repository.Remove(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogOrderSoftDeleted(command.OrderId);
        return Result<TVoid, Error>.Success(TVoid.Instance);
    }

    [LoggerMessage(
        EventId = 3101,
        Level = LogLevel.Information,
        Message = "ChangeOrder {OrderId} soft-deleted.")]
    private partial void LogOrderSoftDeleted(Guid orderId);
}
