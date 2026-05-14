using ChangeOrder.Business.Commands.UpdateOrder;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Errors;
using ChangeOrder.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using DomainChangeOrder = ChangeOrder.Domain.Entities.ChangeOrder;

namespace ChangeOrder.Business.Tests.Commands.UpdateOrder;

/// <summary>
/// Exercises FR-013: two concurrent <see cref="UpdateOrderHandler"/> invocations
/// against the same starting <c>RowVersion</c>. The first save succeeds; the
/// second's <c>SaveChangesWithConcurrencyDetectionAsync</c> resolves to the
/// <c>DomainErrors.Order.ConcurrencyConflict</c> failure that the API maps to
/// HTTP 409. The conflict is simulated at the UoW boundary because the
/// rowversion token is owned by SQL Server and the in-memory EF provider does
/// not generate one.
/// </summary>
public sealed class ConcurrencyTests
{
    private static readonly DateTime FixedNowUtc = new(2026, 5, 13, 10, 0, 0, DateTimeKind.Utc);
    private static readonly byte[] InitialRowVersion = [0x01, 0x02, 0x03, 0x04];

    [Fact]
    public async Task TwoUpdatesWithSameStartingRowVersion_FirstSucceeds_SecondReturns409()
    {
        DomainChangeOrder order = BuildOrder();

        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        repository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DomainChangeOrder?>(order));

        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        int saveCount = 0;
        unitOfWork
            .SaveChangesWithConcurrencyDetectionAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                int call = Interlocked.Increment(ref saveCount);
                return call == 1
                    ? Task.FromResult(Result<int, Error>.Success(1))
                    : Task.FromResult(Result<int, Error>.Failure(DomainErrors.Order.ConcurrencyConflict()));
            });

        UpdateOrderHandler handler = new(repository, unitOfWork, NullLogger<UpdateOrderHandler>.Instance);

        UpdateOrderCommand commandA = BuildCommand(order.Id, programName: "FirstWins");
        UpdateOrderCommand commandB = BuildCommand(order.Id, programName: "SecondLoses");

        Result<TVoid, Error> first = await handler.HandleAsync(commandA, CancellationToken.None);
        Result<TVoid, Error> second = await handler.HandleAsync(commandB, CancellationToken.None);

        first.IsSuccess.Should().BeTrue("the first writer carries a fresh rowversion");
        second.IsFailure.Should().BeTrue("the second writer presents a stale rowversion");
        second.Error!.Code.Should().Be("order.concurrency_conflict");
        saveCount.Should().Be(2);
    }

    [Fact]
    public async Task ConcurrentUpdates_OnlyOneSucceeds_WhenRunInParallel()
    {
        DomainChangeOrder order = BuildOrder();

        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        repository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DomainChangeOrder?>(order));

        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        int saveCount = 0;
        unitOfWork
            .SaveChangesWithConcurrencyDetectionAsync(Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                int call = Interlocked.Increment(ref saveCount);
                // Allow the second invocation to race in before the first
                // resolves; this mirrors a real "two clients hit PUT" pattern.
                await Task.Delay(15).ConfigureAwait(false);
                return call == 1
                    ? Result<int, Error>.Success(1)
                    : Result<int, Error>.Failure(DomainErrors.Order.ConcurrencyConflict());
            });

        UpdateOrderHandler handler = new(repository, unitOfWork, NullLogger<UpdateOrderHandler>.Instance);

        Task<Result<TVoid, Error>> taskA = handler.HandleAsync(BuildCommand(order.Id, "RaceA"), CancellationToken.None);
        Task<Result<TVoid, Error>> taskB = handler.HandleAsync(BuildCommand(order.Id, "RaceB"), CancellationToken.None);

        Result<TVoid, Error>[] outcomes = await Task.WhenAll(taskA, taskB);
        int successCount = outcomes.Count(r => r.IsSuccess);
        int conflictCount = outcomes.Count(r =>
            r.IsFailure && r.Error!.Code == "order.concurrency_conflict");

        successCount.Should().Be(1);
        conflictCount.Should().Be(1);
    }

    private static UpdateOrderCommand BuildCommand(Guid orderId, string programName) => new(
        OrderId: orderId,
        RowVersion: InitialRowVersion,
        ProgramName: programName,
        ProductionVersion: "v1.0.1",
        VersionScreenshotPath: null,
        WorkDescription: "Concurrent update body",
        RequestDetails: "Concurrent details.",
        Justification: "Concurrent justification.",
        RequiredAction: "Concurrent action.",
        RequesterName: "Jane Doe",
        RequesterPosition: "QA Lead",
        RequesterDepartment: "Quality",
        RequesterEmail: "jane.doe@example.com");

    private static DomainChangeOrder BuildOrder()
    {
        Result<OrderNumber, Error> number = OrderNumber.Create(DateOnly.FromDateTime(FixedNowUtc), 1);
        RequesterInfo requester = new("Jane Doe", "QA Lead", "Quality", "jane.doe@example.com");
        ChangeOrderContent content = new(
            "BillingApp",
            "v1.0.0",
            null,
            "Fix rounding bug",
            "Add half-even rounding to totals.",
            "Customer complaints about cents off.",
            "Patch Module B, redeploy.");
        return new DomainChangeOrder(number.Value!, FixedNowUtc, requester, content);
    }
}
