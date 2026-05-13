using ChangeOrder.Business.Commands.RecordMilestoneDates;
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

namespace ChangeOrder.Business.Tests.Commands.RecordMilestoneDates;

/// <summary>
/// Unit tests for <see cref="RecordMilestoneDatesHandler"/>. Covers the state
/// transitions Approved → InProgress (via DeliveryDate) and InProgress →
/// Deployed (via ProductionDeployDate), plus the rejection of out-of-order
/// updates (SC-006 / FR-007 / data-model §8).
/// </summary>
public sealed class RecordMilestoneDatesHandlerTests
{
    private static readonly DateTime FixedNowUtc = new(2026, 5, 13, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HandleAsync_DeliveryDateOnApprovedOrder_AdvancesToInProgress()
    {
        DomainChangeOrder order = BuildOrder();
        ApproveAllSlots(order);
        order.Status.Should().Be(OrderStatus.Approved);

        (RecordMilestoneDatesHandler handler, IUnitOfWork uow) = BuildHandler(order);
        RecordMilestoneDatesCommand command = new(
            OrderId: order.Id,
            DeliveryDate: FixedNowUtc.AddHours(1),
            InitialEvaluationDate: null,
            ProductionDeployDate: null,
            PostDeployScreenshotPath: null);

        Result<TVoid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.InProgress);
        order.DeliveryDate.Should().Be(FixedNowUtc.AddHours(1));
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ProductionDeployOnInProgress_AdvancesToDeployed()
    {
        DomainChangeOrder order = BuildOrder();
        ApproveAllSlots(order);
        order.RecordDeliveryDate(FixedNowUtc.AddHours(1));
        order.Status.Should().Be(OrderStatus.InProgress);

        (RecordMilestoneDatesHandler handler, IUnitOfWork uow) = BuildHandler(order);
        RecordMilestoneDatesCommand command = new(
            OrderId: order.Id,
            DeliveryDate: null,
            InitialEvaluationDate: null,
            ProductionDeployDate: FixedNowUtc.AddHours(2),
            PostDeployScreenshotPath: "/blobs/after.png");

        Result<TVoid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Deployed);
        order.ProductionDeployDate.Should().Be(FixedNowUtc.AddHours(2));
        order.PostDeployScreenshotPath.Should().Be("/blobs/after.png");
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ProductionDeployWhileStillApproved_ReturnsInvalidTransition()
    {
        DomainChangeOrder order = BuildOrder();
        ApproveAllSlots(order);
        order.Status.Should().Be(OrderStatus.Approved);

        (RecordMilestoneDatesHandler handler, IUnitOfWork uow) = BuildHandler(order);
        RecordMilestoneDatesCommand command = new(
            OrderId: order.Id,
            DeliveryDate: null,
            InitialEvaluationDate: null,
            ProductionDeployDate: FixedNowUtc.AddHours(2),
            PostDeployScreenshotPath: null);

        Result<TVoid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("order.invalid_transition");
        order.Status.Should().Be(OrderStatus.Approved);
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_DeliveryAndDeployInSameCall_AdvancesAllTheWayToDeployed()
    {
        DomainChangeOrder order = BuildOrder();
        ApproveAllSlots(order);

        (RecordMilestoneDatesHandler handler, IUnitOfWork uow) = BuildHandler(order);
        RecordMilestoneDatesCommand command = new(
            OrderId: order.Id,
            DeliveryDate: FixedNowUtc.AddHours(1),
            InitialEvaluationDate: FixedNowUtc.AddHours(2),
            ProductionDeployDate: FixedNowUtc.AddHours(3),
            PostDeployScreenshotPath: null);

        Result<TVoid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Deployed);
        order.DeliveryDate.Should().Be(FixedNowUtc.AddHours(1));
        order.InitialEvaluationDate.Should().Be(FixedNowUtc.AddHours(2));
        order.ProductionDeployDate.Should().Be(FixedNowUtc.AddHours(3));
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_OrderNotFound_ReturnsNotFound()
    {
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DomainChangeOrder?>(null));
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        RecordMilestoneDatesHandler handler = new(repository, uow, NullLogger<RecordMilestoneDatesHandler>.Instance);

        RecordMilestoneDatesCommand command = new(
            OrderId: Guid.NewGuid(),
            DeliveryDate: FixedNowUtc.AddHours(1),
            InitialEvaluationDate: null,
            ProductionDeployDate: null,
            PostDeployScreenshotPath: null);

        Result<TVoid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("order.not_found");
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static (RecordMilestoneDatesHandler handler, IUnitOfWork uow) BuildHandler(DomainChangeOrder order)
    {
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        repository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DomainChangeOrder?>(order));

        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        uow.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(1));

        RecordMilestoneDatesHandler handler = new(repository, uow, NullLogger<RecordMilestoneDatesHandler>.Instance);
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
