using ChangeOrder.Business.Abstractions;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Enums;
using ChangeOrder.Domain.Errors;
using Microsoft.Extensions.Logging;
using DomainChangeOrder = ChangeOrder.Domain.Entities.ChangeOrder;

namespace ChangeOrder.Business.Commands.RecordApproval;

/// <summary>
/// Handler for <see cref="RecordApprovalCommand"/>. Loads the aggregate, applies
/// the verdict via <c>ChangeOrder.RecordApproval</c> (which enforces the state
/// machine and may advance <see cref="Domain.Enums.OrderStatus.Draft"/> →
/// <see cref="Domain.Enums.OrderStatus.PendingApproval"/> and
/// <see cref="Domain.Enums.OrderStatus.PendingApproval"/> →
/// <see cref="Domain.Enums.OrderStatus.Approved"/>), then commits via
/// <see cref="IUnitOfWork"/>.
/// </summary>
public sealed partial class RecordApprovalHandler : ICommandHandler<RecordApprovalCommand, Result<TVoid, Error>>
{
    private readonly IChangeOrderRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RecordApprovalHandler> _logger;

    /// <summary>Builds the handler with its three dependencies.</summary>
    public RecordApprovalHandler(
        IChangeOrderRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<RecordApprovalHandler> logger)
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
        RecordApprovalCommand command,
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

        Result<TVoid, Error> transition = order.RecordApproval(command.Level, command.Verdict);
        if (transition.IsFailure)
        {
            LogInvalidTransition(command.OrderId, command.Level, order.Status);
            return transition;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        LogApprovalRecorded(command.OrderId, command.Level, command.Verdict, order.Status);
        return Result<TVoid, Error>.Success(TVoid.Instance);
    }

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Approval recorded on {OrderId}: level={Level}, verdict={Verdict}, statusAfter={Status}.")]
    private partial void LogApprovalRecorded(Guid orderId, ApprovalLevel level, ApprovalStatus verdict, OrderStatus status);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Warning,
        Message = "Approval rejected by state machine on {OrderId}: level={Level}, currentStatus={Status}.")]
    private partial void LogInvalidTransition(Guid orderId, ApprovalLevel level, OrderStatus status);
}
