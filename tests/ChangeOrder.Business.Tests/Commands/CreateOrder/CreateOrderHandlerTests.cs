using ChangeOrder.Business.Commands.CreateOrder;
using ChangeOrder.Business.Services;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Enums;
using ChangeOrder.Domain.Errors;
using ChangeOrder.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using DomainChangeOrder = ChangeOrder.Domain.Entities.ChangeOrder;

namespace ChangeOrder.Business.Tests.Commands.CreateOrder;

public sealed class CreateOrderHandlerTests
{
    private static readonly DateTime FixedNowUtc = new(2026, 5, 13, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HandleAsync_WithValidCommand_ReturnsCreatedOrder()
    {
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        repository.GetNextSequenceForDateAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));
        repository.FindIdempotencyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IdempotencyKey?>(null));

        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        IUnitOfWorkTransaction tx = Substitute.For<IUnitOfWorkTransaction>();
        uow.BeginTransactionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(tx));
        uow.SaveChangesWithDuplicateDetectionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<int, Error>.Success(1)));

        CreateOrderHandler handler = BuildHandler(repository, uow);
        CreateOrderCommand command = BuildCommand();

        Result<CreateOrderResult, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.WasReplay.Should().BeFalse();
        result.Value.Order.Status.Should().Be(OrderStatus.Draft);
        result.Value.Order.OrderNumber.Value.Should().Be("20260513-01");
        await repository.Received(1).AddAsync(Arg.Any<DomainChangeOrder>(), Arg.Any<CancellationToken>());
        await repository.Received(1).AddIdempotencyAsync(Arg.Any<IdempotencyKey>(), Arg.Any<CancellationToken>());
        await uow.Received(1).BeginTransactionAsync(Arg.Any<CancellationToken>());
        await tx.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithInvalidCommand_ReturnsValidationError()
    {
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        CreateOrderHandler handler = BuildHandler(repository, uow);
        CreateOrderCommand command = BuildCommand() with { RequesterEmail = "not-an-email" };

        Result<CreateOrderResult, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("validation.error");
        await repository.DidNotReceive().AddAsync(Arg.Any<DomainChangeOrder>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithIdempotentReplay_ReturnsExistingOrderWithWasReplayTrue()
    {
        CreateOrderCommand command = BuildCommand();
        byte[] hash = IdempotencyService.ComputeRequestHash(command);
        Guid existingId = Guid.NewGuid();
        IdempotencyKey persistedKey = new(command.IdempotencyKey, existingId, hash, FixedNowUtc);
        DomainChangeOrder existingOrder = BuildExistingOrder(existingId);

        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        repository.FindIdempotencyAsync(command.IdempotencyKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IdempotencyKey?>(persistedKey));
        repository.GetByIdAsync(existingId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DomainChangeOrder?>(existingOrder));

        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        CreateOrderHandler handler = BuildHandler(repository, uow);

        Result<CreateOrderResult, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.WasReplay.Should().BeTrue();
        result.Value.Order.Id.Should().Be(existingId);
        await repository.DidNotReceive().AddAsync(Arg.Any<DomainChangeOrder>(), Arg.Any<CancellationToken>());
        await uow.DidNotReceive().SaveChangesWithDuplicateDetectionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_DuplicateOrderNumberOnFirstAttempt_RetriesAndSucceeds()
    {
        // Simulates the UNIQUE-violation retry loop: first SaveChanges fails with
        // DuplicateNumber (as UoW returns after a 2601/2627 SqlException), the
        // second attempt succeeds. Pre-fix this used to throw because the
        // IdempotencyKey from the failed attempt stayed tracked as Added and
        // re-adding it tripped EF's IdentityMap. Post-fix, UoW clears the
        // ChangeTracker on UNIQUE violation so the handler can retry cleanly.
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        int[] sequences = [1, 2];
        int sequenceCall = 0;
        repository.GetNextSequenceForDateAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(sequences[sequenceCall++]));
        repository.FindIdempotencyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IdempotencyKey?>(null));

        Result<int, Error>[] saveResults =
        [
            Result<int, Error>.Failure(DomainErrors.Order.DuplicateNumber("20260513-01")),
            Result<int, Error>.Success(1)
        ];
        int saveCall = 0;
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        IUnitOfWorkTransaction tx = Substitute.For<IUnitOfWorkTransaction>();
        uow.BeginTransactionAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(tx));
        uow.SaveChangesWithDuplicateDetectionAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(saveResults[saveCall++]));

        CreateOrderHandler handler = BuildHandler(repository, uow);
        CreateOrderCommand command = BuildCommand();

        Result<CreateOrderResult, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.WasReplay.Should().BeFalse();
        await repository.Received(2).AddAsync(Arg.Any<DomainChangeOrder>(), Arg.Any<CancellationToken>());
        await repository.Received(2).AddIdempotencyAsync(Arg.Any<IdempotencyKey>(), Arg.Any<CancellationToken>());
        await uow.Received(2).SaveChangesWithDuplicateDetectionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_OpensTransactionPerAttempt_CommitsOnlyOnSuccess()
    {
        // Verifies R-1 / Opción A: each retry iteration opens its own
        // transaction (so the UPDLOCK+HOLDLOCK read and the INSERT share a
        // single physical transaction), and Commit is invoked ONLY on the
        // successful attempt. Failed attempts must NOT commit; the
        // `await using` scope auto-rolls back when leaving the loop body.
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        int[] sequences = [1, 2];
        int sequenceCall = 0;
        repository.GetNextSequenceForDateAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(sequences[sequenceCall++]));
        repository.FindIdempotencyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IdempotencyKey?>(null));

        Result<int, Error>[] saveResults =
        [
            Result<int, Error>.Failure(DomainErrors.Order.DuplicateNumber("20260513-01")),
            Result<int, Error>.Success(1)
        ];
        int saveCall = 0;
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        IUnitOfWorkTransaction failedTx = Substitute.For<IUnitOfWorkTransaction>();
        IUnitOfWorkTransaction successTx = Substitute.For<IUnitOfWorkTransaction>();
        IUnitOfWorkTransaction[] transactions = [failedTx, successTx];
        int txCall = 0;
        uow.BeginTransactionAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(transactions[txCall++]));
        uow.SaveChangesWithDuplicateDetectionAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(saveResults[saveCall++]));

        CreateOrderHandler handler = BuildHandler(repository, uow);
        CreateOrderCommand command = BuildCommand();

        Result<CreateOrderResult, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await uow.Received(2).BeginTransactionAsync(Arg.Any<CancellationToken>());
        await failedTx.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await successTx.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        // The `await using` scope must dispose both transactions; the failed
        // one auto-rolls back on dispose, the successful one is already
        // committed.
        await failedTx.Received(1).DisposeAsync();
        await successTx.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task HandleAsync_AllAttemptsFail_ReturnsLastFailureAndNeverCommits()
    {
        // When every retry attempt fails with a UNIQUE collision, the handler
        // must surface the last DuplicateNumber failure and NEVER commit any
        // transaction.
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        int sequenceCall = 0;
        repository.GetNextSequenceForDateAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(++sequenceCall));
        repository.FindIdempotencyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IdempotencyKey?>(null));

        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        IUnitOfWorkTransaction tx = Substitute.For<IUnitOfWorkTransaction>();
        uow.BeginTransactionAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(tx));
        uow.SaveChangesWithDuplicateDetectionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<int, Error>.Failure(
                DomainErrors.Order.DuplicateNumber("20260513-99"))));

        CreateOrderHandler handler = BuildHandler(repository, uow);
        CreateOrderCommand command = BuildCommand();

        Result<CreateOrderResult, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("order.duplicate_number");
        await uow.Received(3).BeginTransactionAsync(Arg.Any<CancellationToken>());
        await tx.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithPayloadDivergence_ReturnsPayloadDivergenceError()
    {
        CreateOrderCommand command = BuildCommand();
        byte[] divergentHash = new byte[32];
        Array.Fill(divergentHash, (byte)0xAB);
        IdempotencyKey persistedKey = new(command.IdempotencyKey, Guid.NewGuid(), divergentHash, FixedNowUtc);

        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        repository.FindIdempotencyAsync(command.IdempotencyKey, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IdempotencyKey?>(persistedKey));

        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        CreateOrderHandler handler = BuildHandler(repository, uow);

        Result<CreateOrderResult, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("idempotency.payload_divergence");
        await repository.DidNotReceive().AddAsync(Arg.Any<DomainChangeOrder>(), Arg.Any<CancellationToken>());
    }

    private static CreateOrderHandler BuildHandler(IChangeOrderRepository repository, IUnitOfWork uow)
    {
        IdempotencyService idempotency = new(repository);
        OrderNumberGenerator generator = new(repository);
        TimeProvider time = new FixedTimeProvider(FixedNowUtc);
        return new CreateOrderHandler(
            idempotency,
            generator,
            repository,
            uow,
            NullLogger<CreateOrderHandler>.Instance,
            time);
    }

    private static CreateOrderCommand BuildCommand()
        => new(
            IdempotencyKey: "abc12345-key",
            ProgramName: "BillingApp",
            ProductionVersion: "v1.0.0",
            VersionScreenshotPath: null,
            WorkDescription: "Fix rounding bug",
            RequestDetails: "Add half-even rounding to totals.",
            Justification: "Customer complaints about cents off.",
            RequiredAction: "Patch Module B, redeploy.",
            RequesterName: "Jane Doe",
            RequesterPosition: "QA Lead",
            RequesterDepartment: "Quality",
            RequesterEmail: "jane.doe@example.com");

    private static DomainChangeOrder BuildExistingOrder(Guid id)
    {
        Result<OrderNumber, Error> number = OrderNumber.Create(DateOnly.FromDateTime(FixedNowUtc), 1);
        RequesterInfo requester = new("Jane Doe", "QA Lead", "Quality", "jane.doe@example.com");
        ChangeOrderContent content = new(
            "BillingApp", "v1.0.0", null,
            "Fix rounding bug",
            "Add half-even rounding to totals.",
            "Customer complaints about cents off.",
            "Patch Module B, redeploy.");
        DomainChangeOrder order = new(number.Value!, FixedNowUtc, requester, content);
        typeof(DomainChangeOrder)
            .GetProperty(nameof(DomainChangeOrder.Id))!
            .SetValue(order, id);
        return order;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FixedTimeProvider(DateTime utcNow) => _utcNow = new DateTimeOffset(utcNow, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
