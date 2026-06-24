using ChangeOrder.Business.Commands.SetApproval;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Enums;
using ChangeOrder.Domain.Errors;
using FluentAssertions;
using NSubstitute;

namespace ChangeOrder.Business.Tests.Commands;

/// <summary>
/// Tests unitarios para <see cref="SetApprovalHandler"/>.
/// </summary>
public sealed class SetApprovalHandlerTests
{
    /// <summary>
    /// Nivel "requester" con veredicto Approved actualiza RequesterApproval correctamente.
    /// </summary>
    [Fact]
    public async Task HandleAsync_OrderExists_RequesterLevel_SetsApproval()
    {
        // Arrange
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        SetApprovalHandler handler = new(repository, unitOfWork);

        ChangeOrderEntity order = OrderBuilder.Build();

        repository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>())
            .Returns(order);

        SetApprovalCommand command = new(Id: order.Id, Level: "requester", Verdict: "Approved");

        // Act
        Result<Guid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(order.Id);
        order.Approval.RequesterApproval.Should().Be(ApprovalStatus.Approved);
        repository.Received(1).Update(order);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Nivel "departmentHead" con veredicto Approved actualiza DepartmentHeadApproval.
    /// </summary>
    [Fact]
    public async Task HandleAsync_OrderExists_DepartmentHeadLevel_SetsApproval()
    {
        // Arrange
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        SetApprovalHandler handler = new(repository, unitOfWork);

        ChangeOrderEntity order = OrderBuilder.Build();

        repository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>())
            .Returns(order);

        SetApprovalCommand command = new(Id: order.Id, Level: "departmentHead", Verdict: "Rejected");

        // Act
        Result<Guid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Approval.DepartmentHeadApproval.Should().Be(ApprovalStatus.Rejected);
    }

    /// <summary>
    /// Cuando la orden no existe, retorna NotFound sin persistir.
    /// </summary>
    [Fact]
    public async Task HandleAsync_OrderNotFound_ReturnsNotFoundError()
    {
        // Arrange
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        SetApprovalHandler handler = new(repository, unitOfWork);

        Guid id = Guid.NewGuid();
        repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((ChangeOrderEntity?)null);

        SetApprovalCommand command = new(Id: id, Level: "requester", Verdict: "Approved");

        // Act
        Result<Guid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(DomainErrors.Order.NotFound.Code);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Un Level desconocido no cambia ningún nivel de aprobación (retorna el mismo ApprovalChain).
    /// </summary>
    [Fact]
    public async Task HandleAsync_InvalidLevel_DoesNotChangeApproval()
    {
        // Arrange
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        SetApprovalHandler handler = new(repository, unitOfWork);

        ChangeOrderEntity order = OrderBuilder.Build();
        var originalApproval = order.Approval;

        repository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>())
            .Returns(order);

        SetApprovalCommand command = new(Id: order.Id, Level: "unknownLevel", Verdict: "Approved");

        // Act
        Result<Guid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.Approval.Should().Be(originalApproval);
    }
}
