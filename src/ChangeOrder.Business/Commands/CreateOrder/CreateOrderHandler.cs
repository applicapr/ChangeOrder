using ChangeOrder.Business.Abstractions;
using ChangeOrder.Business.Services;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Errors;
using ChangeOrder.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using DomainChangeOrder = ChangeOrder.Domain.Entities.ChangeOrder;

namespace ChangeOrder.Business.Commands.CreateOrder;

/// <summary>
/// Handler for <see cref="CreateOrderCommand"/>. Orchestrates the full US1
/// path: validation → idempotency lookup → next-sequence allocation →
/// aggregate construction → transactional persistence with UNIQUE-violation
/// retry (research.md R-1 + R-2). Returns the aggregate and a replay flag so
/// the Presentation layer can pick between 201 Created and 200 OK.
/// </summary>
public sealed partial class CreateOrderHandler : ICommandHandler<CreateOrderCommand, Result<CreateOrderResult, Error>>
{
    private const int MaxRetryAttempts = 3;

    private readonly IdempotencyService _idempotencyService;
    private readonly OrderNumberGenerator _orderNumberGenerator;
    private readonly IChangeOrderRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateOrderHandler> _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>Builds the handler with every dependency it needs to run.</summary>
    public CreateOrderHandler(
        IdempotencyService idempotencyService,
        OrderNumberGenerator orderNumberGenerator,
        IChangeOrderRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<CreateOrderHandler> logger,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(idempotencyService);
        ArgumentNullException.ThrowIfNull(orderNumberGenerator);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _idempotencyService = idempotencyService;
        _orderNumberGenerator = orderNumberGenerator;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async Task<Result<CreateOrderResult, Error>> HandleAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<TVoid, Error> validation = CreateOrderValidator.Validate(command);
        if (validation.IsFailure)
        {
            return Result<CreateOrderResult, Error>.Failure(validation.Error!);
        }

        IdempotencyOutcome outcome = await _idempotencyService
            .ResolveAsync(command.IdempotencyKey, command, cancellationToken)
            .ConfigureAwait(false);

        return outcome switch
        {
            IdempotencyOutcome.Existing existing
                => await ReplayAsync(existing.OrderId, cancellationToken).ConfigureAwait(false),
            IdempotencyOutcome.Conflict
                => Result<CreateOrderResult, Error>.Failure(
                    DomainErrors.Idempotency.PayloadDivergence(command.IdempotencyKey)),
            IdempotencyOutcome.Fresh fresh
                => await CreateFreshAsync(command, fresh.Hash, cancellationToken).ConfigureAwait(false),
            _ => Result<CreateOrderResult, Error>.Failure(
                new Error("idempotency.unknown_outcome", "Unexpected idempotency outcome."))
        };
    }

    private async Task<Result<CreateOrderResult, Error>> ReplayAsync(Guid orderId, CancellationToken cancellationToken)
    {
        DomainChangeOrder? existing = await _repository
            .GetByIdAsync(orderId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return Result<CreateOrderResult, Error>.Failure(DomainErrors.Order.NotFound(orderId));
        }

        return Result<CreateOrderResult, Error>.Success(new CreateOrderResult(existing, WasReplay: true));
    }

    private async Task<Result<CreateOrderResult, Error>> CreateFreshAsync(
        CreateOrderCommand command,
        byte[] requestHash,
        CancellationToken cancellationToken)
    {
        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        DateOnly today = DateOnly.FromDateTime(nowUtc);

        for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            Result<OrderNumber, Error> numberResult = await _orderNumberGenerator
                .GenerateAsync(today, cancellationToken)
                .ConfigureAwait(false);

            if (numberResult.IsFailure)
            {
                return Result<CreateOrderResult, Error>.Failure(numberResult.Error!);
            }

            DomainChangeOrder order = BuildOrder(numberResult.Value!, command, nowUtc);
            IdempotencyKey idempotency = new(command.IdempotencyKey, order.Id, requestHash, nowUtc);

            await _repository.AddAsync(order, cancellationToken).ConfigureAwait(false);
            await _repository.AddIdempotencyAsync(idempotency, cancellationToken).ConfigureAwait(false);

            Result<int, Error> saveResult = await _unitOfWork
                .SaveChangesWithDuplicateDetectionAsync(cancellationToken)
                .ConfigureAwait(false);

            if (saveResult.IsSuccess)
            {
                return Result<CreateOrderResult, Error>.Success(new CreateOrderResult(order, WasReplay: false));
            }

            LogUniqueCollision(attempt, MaxRetryAttempts, numberResult.Value!.Value);
        }

        return Result<CreateOrderResult, Error>.Failure(DomainErrors.Order.DailySequenceExhausted(today));
    }

    private static DomainChangeOrder BuildOrder(OrderNumber orderNumber, CreateOrderCommand command, DateTime nowUtc)
    {
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

        return new DomainChangeOrder(orderNumber, nowUtc, requester, content);
    }

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "OrderNumber UNIQUE collision on attempt {Attempt}/{Max} for {OrderNumber}; retrying.")]
    private partial void LogUniqueCollision(int attempt, int max, string orderNumber);
}
