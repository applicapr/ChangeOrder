using ChangeOrder.Business.Abstractions;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Enums;
using ChangeOrder.Domain.Errors;
using Microsoft.Extensions.Logging;
using DomainChangeOrder = ChangeOrder.Domain.Entities.ChangeOrder;

namespace ChangeOrder.Business.Commands.RecordMilestoneDates;

/// <summary>
/// Handler for <see cref="RecordMilestoneDatesCommand"/>. Loads the aggregate
/// and applies each provided milestone date through the domain state machine,
/// in the canonical order Delivery → InitialEvaluation → ProductionDeploy. The
/// first failed transition aborts and is returned; nothing is persisted in that
/// case (no <see cref="IUnitOfWork.SaveChangesAsync"/> call).
/// </summary>
public sealed partial class RecordMilestoneDatesHandler
    : ICommandHandler<RecordMilestoneDatesCommand, Result<TVoid, Error>>
{
    private readonly IChangeOrderRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RecordMilestoneDatesHandler> _logger;

    /// <summary>Builds the handler with its three dependencies.</summary>
    public RecordMilestoneDatesHandler(
        IChangeOrderRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<RecordMilestoneDatesHandler> logger)
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
        RecordMilestoneDatesCommand command,
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

        if (command.DeliveryDate is DateTime deliveryDate)
        {
            Result<TVoid, Error> step = order.RecordDeliveryDate(deliveryDate);
            if (step.IsFailure)
            {
                LogInvalidTransition(command.OrderId, "DeliveryDate", order.Status);
                return step;
            }
        }

        if (command.InitialEvaluationDate is DateTime evaluationDate)
        {
            Result<TVoid, Error> step = order.RecordInitialEvaluationDate(evaluationDate);
            if (step.IsFailure)
            {
                LogInvalidTransition(command.OrderId, "InitialEvaluationDate", order.Status);
                return step;
            }
        }

        if (command.ProductionDeployDate is DateTime deployDate)
        {
            Result<TVoid, Error> step = order.RecordProductionDeploy(deployDate, command.PostDeployScreenshotPath);
            if (step.IsFailure)
            {
                LogInvalidTransition(command.OrderId, "ProductionDeployDate", order.Status);
                return step;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        LogMilestonesRecorded(command.OrderId, order.Status);
        return Result<TVoid, Error>.Success(TVoid.Instance);
    }

    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Information,
        Message = "Milestone dates recorded on {OrderId}: statusAfter={Status}.")]
    private partial void LogMilestonesRecorded(Guid orderId, OrderStatus status);

    [LoggerMessage(
        EventId = 2102,
        Level = LogLevel.Warning,
        Message = "Milestone-date update rejected by state machine on {OrderId}: milestone={Milestone}, currentStatus={Status}.")]
    private partial void LogInvalidTransition(Guid orderId, string milestone, OrderStatus status);
}
