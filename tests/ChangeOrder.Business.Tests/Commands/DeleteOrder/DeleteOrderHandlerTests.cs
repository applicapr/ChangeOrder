using ChangeOrder.Business.Commands.DeleteOrder;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Errors;
using ChangeOrder.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using DomainChangeOrder = ChangeOrder.Domain.Entities.ChangeOrder;

namespace ChangeOrder.Business.Tests.Commands.DeleteOrder;

/// <summary>
/// Unit tests for <see cref="DeleteOrderHandler"/>. The handler is thin —
/// the actual flip from <c>EntityState.Deleted</c> to soft-delete happens in
/// the Data layer's <c>AuditInterceptor</c>; here we only verify the contract
/// between the handler and the repository/UoW.
/// </summary>
public sealed class DeleteOrderHandlerTests
{
    private static readonly DateTime FixedNowUtc = new(2026, 5, 13, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HandleAsync_ExistingOrder_RemovesAndPersists()
    {
        DomainChangeOrder order = BuildOrder();
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        repository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DomainChangeOrder?>(order));
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        uow.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(1));

        DeleteOrderHandler handler = new(repository, uow, NullLogger<DeleteOrderHandler>.Instance);
        DeleteOrderCommand command = new(order.Id);

        Result<TVoid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Received(1).Remove(order);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_MissingOrder_ReturnsNotFoundAndSkipsPersistence()
    {
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DomainChangeOrder?>(null));
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();

        DeleteOrderHandler handler = new(repository, uow, NullLogger<DeleteOrderHandler>.Instance);
        Guid missingId = Guid.NewGuid();
        DeleteOrderCommand command = new(missingId);

        Result<TVoid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("order.not_found");
        repository.DidNotReceive().Remove(Arg.Any<DomainChangeOrder>());
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SecondCallAfterDeletion_ReturnsNotFound()
    {
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DomainChangeOrder?>(null));
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();

        DeleteOrderHandler handler = new(repository, uow, NullLogger<DeleteOrderHandler>.Instance);
        Guid id = Guid.NewGuid();
        DeleteOrderCommand command = new(id);

        // Second invocation: the global query filter hides the soft-deleted row, so the repository returns null.
        Result<TVoid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("order.not_found");
    }

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
