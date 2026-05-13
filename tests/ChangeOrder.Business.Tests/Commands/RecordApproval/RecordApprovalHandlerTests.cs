using ChangeOrder.Business.Commands.RecordApproval;
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

namespace ChangeOrder.Business.Tests.Commands.RecordApproval;

/// <summary>
/// Unit tests for <see cref="RecordApprovalHandler"/>. Covers SC-006 (illegal
/// transitions) and the full <c>(currentChain, level, verdict)</c> matrix
/// described in tasks.md T069.
/// </summary>
public sealed class RecordApprovalHandlerTests
{
    private static readonly DateTime FixedNowUtc = new(2026, 5, 13, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HandleAsync_FirstApprovalOnDraftOrder_AdvancesToPendingApproval()
    {
        DomainChangeOrder order = BuildOrder();
        order.Status.Should().Be(OrderStatus.Draft);

        (RecordApprovalHandler handler, IUnitOfWork uow) = BuildHandler(order);
        RecordApprovalCommand command = new(order.Id, ApprovalLevel.Requester, ApprovalStatus.Approved);

        Result<TVoid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.PendingApproval);
        order.ApprovalChain.RequesterApproval.Should().Be(ApprovalStatus.Approved);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_FinalApprovalCompletesChain_AdvancesToApproved()
    {
        DomainChangeOrder order = BuildOrder();
        order.RecordApproval(ApprovalLevel.Requester, ApprovalStatus.Approved);
        order.RecordApproval(ApprovalLevel.DepartmentHead, ApprovalStatus.Approved);
        order.RecordApproval(ApprovalLevel.ItHead, ApprovalStatus.Approved);
        order.Status.Should().Be(OrderStatus.PendingApproval);

        (RecordApprovalHandler handler, IUnitOfWork uow) = BuildHandler(order);
        RecordApprovalCommand command = new(order.Id, ApprovalLevel.ProgrammingDivision, ApprovalStatus.Approved);

        Result<TVoid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Approved);
        order.ApprovalChain.AllApproved().Should().BeTrue();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_RejectionOnSecondSlot_KeepsPendingApprovalAndPersistsRejected()
    {
        DomainChangeOrder order = BuildOrder();
        order.RecordApproval(ApprovalLevel.Requester, ApprovalStatus.Approved);
        order.Status.Should().Be(OrderStatus.PendingApproval);

        (RecordApprovalHandler handler, IUnitOfWork uow) = BuildHandler(order);
        RecordApprovalCommand command = new(order.Id, ApprovalLevel.DepartmentHead, ApprovalStatus.Rejected);

        Result<TVoid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.PendingApproval);
        order.ApprovalChain.DepartmentHeadApproval.Should().Be(ApprovalStatus.Rejected);
        order.ApprovalChain.AnyRejected().Should().BeTrue();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_OrderAlreadyApproved_ReturnsInvalidTransition()
    {
        DomainChangeOrder order = BuildOrder();
        ApproveAllSlots(order);
        order.Status.Should().Be(OrderStatus.Approved);

        (RecordApprovalHandler handler, IUnitOfWork uow) = BuildHandler(order);
        RecordApprovalCommand command = new(order.Id, ApprovalLevel.Requester, ApprovalStatus.Pending);

        Result<TVoid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("order.invalid_transition");
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_OrderAlreadyDeployed_ReturnsInvalidTransition()
    {
        DomainChangeOrder order = BuildOrder();
        ApproveAllSlots(order);
        order.RecordDeliveryDate(FixedNowUtc.AddHours(1));
        order.RecordProductionDeploy(FixedNowUtc.AddHours(2), postDeployScreenshotPath: null);
        order.Status.Should().Be(OrderStatus.Deployed);

        (RecordApprovalHandler handler, IUnitOfWork uow) = BuildHandler(order);
        RecordApprovalCommand command = new(order.Id, ApprovalLevel.Requester, ApprovalStatus.Approved);

        Result<TVoid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("order.invalid_transition");
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_OrderNotFound_ReturnsNotFound()
    {
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DomainChangeOrder?>(null));
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        RecordApprovalHandler handler = new(repository, uow, NullLogger<RecordApprovalHandler>.Instance);

        Guid missingId = Guid.NewGuid();
        RecordApprovalCommand command = new(missingId, ApprovalLevel.Requester, ApprovalStatus.Approved);

        Result<TVoid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("order.not_found");
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ApprovalOnCancelledOrder_ReturnsInvalidTransition()
    {
        DomainChangeOrder order = BuildOrder();
        order.Cancel();
        order.Status.Should().Be(OrderStatus.Cancelled);

        (RecordApprovalHandler handler, IUnitOfWork uow) = BuildHandler(order);
        RecordApprovalCommand command = new(order.Id, ApprovalLevel.ItHead, ApprovalStatus.Approved);

        Result<TVoid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("order.invalid_transition");
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static (RecordApprovalHandler handler, IUnitOfWork uow) BuildHandler(DomainChangeOrder order)
    {
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        repository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DomainChangeOrder?>(order));

        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        uow.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(1));

        RecordApprovalHandler handler = new(repository, uow, NullLogger<RecordApprovalHandler>.Instance);
        return (handler, uow);
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

    private static void ApproveAllSlots(DomainChangeOrder order)
    {
        order.RecordApproval(ApprovalLevel.Requester, ApprovalStatus.Approved);
        order.RecordApproval(ApprovalLevel.DepartmentHead, ApprovalStatus.Approved);
        order.RecordApproval(ApprovalLevel.ItHead, ApprovalStatus.Approved);
        order.RecordApproval(ApprovalLevel.ProgrammingDivision, ApprovalStatus.Approved);
    }
}
