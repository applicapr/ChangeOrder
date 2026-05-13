using ChangeOrder.Business.Abstractions;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Errors;
using ChangeOrder.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using DomainChangeOrder = ChangeOrder.Domain.Entities.ChangeOrder;

namespace ChangeOrder.Business.Commands.UpdateOrder;

/// <summary>
/// Handler for <see cref="UpdateOrderCommand"/>. Loads the aggregate, attaches
/// the client-supplied <c>rowversion</c> for optimistic-concurrency checking
/// (FR-013), invokes <see cref="DomainChangeOrder.UpdateContent"/> (FR-006 —
/// only allowed in <c>Draft</c>) and commits via
/// <see cref="IUnitOfWork.SaveChangesWithConcurrencyDetectionAsync"/>. The
/// rowversion mismatch is translated to <c>order.concurrency_conflict</c>
/// (HTTP 409) inside the Data layer.
/// </summary>
public sealed partial class UpdateOrderHandler : ICommandHandler<UpdateOrderCommand, Result<TVoid, Error>>
{
    private readonly IChangeOrderRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateOrderHandler> _logger;

    /// <summary>Builds the handler with its three dependencies.</summary>
    public UpdateOrderHandler(
        IChangeOrderRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateOrderHandler> logger)
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
        UpdateOrderCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<TVoid, Error> validation = UpdateOrderValidator.Validate(command);
        if (validation.IsFailure)
        {
            return validation;
        }

        DomainChangeOrder? order = await _repository
            .GetByIdAsync(command.OrderId, cancellationToken)
            .ConfigureAwait(false);

        if (order is null)
        {
            return Result<TVoid, Error>.Failure(DomainErrors.Order.NotFound(command.OrderId));
        }

        order.AttachConcurrencyToken(command.RowVersion);

        RequesterInfo requester = new(
            command.RequesterName,
            command.RequesterPosition,
            command.RequesterDepartment,
            command.RequesterEmail);

        ChangeOrderContent content = new(
            command.ProgramName,
            command.ProductionVersion,
            command.VersionScreenshotPath,
            command.WorkDescription,
            command.RequestDetails,
            command.Justification,
            command.RequiredAction);

        Result<TVoid, Error> transition = order.UpdateContent(requester, content);
        if (transition.IsFailure)
        {
            LogEditAfterDraft(command.OrderId, order.Status.ToString());
            return transition;
        }

        Result<int, Error> save = await _unitOfWork
            .SaveChangesWithConcurrencyDetectionAsync(cancellationToken)
            .ConfigureAwait(false);

        if (save.IsFailure)
        {
            LogConcurrencyConflict(command.OrderId);
            return Result<TVoid, Error>.Failure(save.Error!);
        }

        LogOrderUpdated(command.OrderId);
        return Result<TVoid, Error>.Success(TVoid.Instance);
    }

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "ChangeOrder {OrderId} updated in Draft.")]
    private partial void LogOrderUpdated(Guid orderId);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Warning,
        Message = "Edit rejected on {OrderId}: order is in {Status} (FR-006).")]
    private partial void LogEditAfterDraft(Guid orderId, string status);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Warning,
        Message = "Concurrency conflict on {OrderId}: client rowversion did not match (FR-013).")]
    private partial void LogConcurrencyConflict(Guid orderId);
}
