using ChangeOrder.Business.Commands.UpdateOrder;
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

namespace ChangeOrder.Business.Tests.Commands.UpdateOrder;

/// <summary>
/// Unit tests for <see cref="UpdateOrderHandler"/>. Covers FR-006 (Draft-only)
/// and FR-013 (optimistic concurrency) plus the not-found / validation paths.
/// </summary>
public sealed class UpdateOrderHandlerTests
{
    private static readonly DateTime FixedNowUtc = new(2026, 5, 13, 10, 0, 0, DateTimeKind.Utc);
    private static readonly byte[] DefaultRowVersion = [0x01, 0x02, 0x03, 0x04];

    [Fact]
    public async Task HandleAsync_DraftOrder_UpdatesContentAndPersists()
    {
        DomainChangeOrder order = BuildOrder();
        order.Status.Should().Be(OrderStatus.Draft);

        (UpdateOrderHandler handler, IUnitOfWork uow) = BuildHandler(order, savesSuccessfully: true);
        UpdateOrderCommand command = BuildCommand(order.Id, programName: "UpdatedApp");

        Result<TVoid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.ProgramName.Should().Be("UpdatedApp");
        await uow.Received(1).SaveChangesWithConcurrencyDetectionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_PendingApprovalOrder_ReturnsEditAfterDraft()
    {
        DomainChangeOrder order = BuildOrder();
        order.RecordApproval(ApprovalLevel.Requester, ApprovalStatus.Approved);
        order.Status.Should().Be(OrderStatus.PendingApproval);

        (UpdateOrderHandler handler, IUnitOfWork uow) = BuildHandler(order, savesSuccessfully: true);
        UpdateOrderCommand command = BuildCommand(order.Id);

        Result<TVoid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("order.edit_after_draft");
        await uow.DidNotReceive().SaveChangesWithConcurrencyDetectionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ApprovedOrder_ReturnsEditAfterDraft()
    {
        DomainChangeOrder order = BuildOrder();
        ApproveAllSlots(order);
        order.Status.Should().Be(OrderStatus.Approved);

        (UpdateOrderHandler handler, IUnitOfWork uow) = BuildHandler(order, savesSuccessfully: true);
        UpdateOrderCommand command = BuildCommand(order.Id);

        Result<TVoid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("order.edit_after_draft");
    }

    [Fact]
    public async Task HandleAsync_StaleRowVersion_ReturnsConcurrencyConflict()
    {
        DomainChangeOrder order = BuildOrder();
        (UpdateOrderHandler handler, IUnitOfWork uow) = BuildHandler(order, savesSuccessfully: false);
        UpdateOrderCommand command = BuildCommand(order.Id);

        Result<TVoid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("order.concurrency_conflict");
        await uow.Received(1).SaveChangesWithConcurrencyDetectionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_OrderNotFound_ReturnsNotFound()
    {
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DomainChangeOrder?>(null));
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        UpdateOrderHandler handler = new(repository, uow, NullLogger<UpdateOrderHandler>.Instance);

        Guid missingId = Guid.NewGuid();
        UpdateOrderCommand command = BuildCommand(missingId);

        Result<TVoid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("order.not_found");
        await uow.DidNotReceive().SaveChangesWithConcurrencyDetectionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_BlankRequesterEmail_ReturnsValidationError()
    {
        DomainChangeOrder order = BuildOrder();
        (UpdateOrderHandler handler, IUnitOfWork uow) = BuildHandler(order, savesSuccessfully: true);
        UpdateOrderCommand command = BuildCommand(order.Id) with { RequesterEmail = "not-an-email" };

        Result<TVoid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("validation.error");
        await uow.DidNotReceive().SaveChangesWithConcurrencyDetectionAsync(Arg.Any<CancellationToken>());
    }

    private static (UpdateOrderHandler handler, IUnitOfWork uow) BuildHandler(DomainChangeOrder order, bool savesSuccessfully)
    {
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        repository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DomainChangeOrder?>(order));

        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        Result<int, Error> saveResult = savesSuccessfully
            ? Result<int, Error>.Success(1)
            : Result<int, Error>.Failure(DomainErrors.Order.ConcurrencyConflict());
        uow.SaveChangesWithConcurrencyDetectionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(saveResult));

        UpdateOrderHandler handler = new(repository, uow, NullLogger<UpdateOrderHandler>.Instance);
        return (handler, uow);
    }

    private static UpdateOrderCommand BuildCommand(Guid orderId, string programName = "BillingApp") => new(
        OrderId: orderId,
        RowVersion: DefaultRowVersion,
        ProgramName: programName,
        ProductionVersion: "v1.0.1",
        VersionScreenshotPath: null,
        WorkDescription: "Updated description",
        RequestDetails: "Updated details.",
        Justification: "Updated justification.",
        RequiredAction: "Updated action.",
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

    private static void ApproveAllSlots(DomainChangeOrder order)
    {
        order.RecordApproval(ApprovalLevel.Requester, ApprovalStatus.Approved);
        order.RecordApproval(ApprovalLevel.DepartmentHead, ApprovalStatus.Approved);
        order.RecordApproval(ApprovalLevel.ItHead, ApprovalStatus.Approved);
        order.RecordApproval(ApprovalLevel.ProgrammingDivision, ApprovalStatus.Approved);
    }
}
