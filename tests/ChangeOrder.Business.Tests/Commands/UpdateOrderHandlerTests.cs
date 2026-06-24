using ChangeOrder.Business.Commands.UpdateOrder;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Enums;
using ChangeOrder.Domain.Errors;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace ChangeOrder.Business.Tests.Commands;

/// <summary>
/// Tests unitarios para <see cref="UpdateOrderHandler"/>.
/// </summary>
public sealed class UpdateOrderHandlerTests
{
    private static UpdateOrderCommand BuildCommand(Guid id) => new(
        Id: id,
        ProgramName: "UpdatedApp",
        ProductionVersion: "v2.0.0",
        VersionScreenshotPath: null,
        WorkDescription: "Updated work",
        RequestDetails: "Updated details",
        Justification: "Updated justification",
        RequiredAction: "Updated action",
        DeliveryDate: null,
        Status: OrderStatus.PendingApproval,
        RowVersion: Array.Empty<byte>()
    );

    /// <summary>
    /// Cuando la orden existe, actualiza los campos y retorna el Id.
    /// </summary>
    [Fact]
    public async Task HandleAsync_OrderExists_UpdatesOrderAndReturnsId()
    {
        // Arrange
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        UpdateOrderHandler handler = new(repository, unitOfWork);

        ChangeOrderEntity order = OrderBuilder.Build();
        UpdateOrderCommand command = BuildCommand(order.Id);

        repository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>())
            .Returns(order);

        // Act
        Result<Guid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(order.Id);
        repository.Received(1).UpdateWithConcurrencyToken(order, command.RowVersion);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Cuando la orden no existe, retorna NotFound sin intentar guardar.
    /// </summary>
    [Fact]
    public async Task HandleAsync_OrderNotFound_ReturnsNotFoundError()
    {
        // Arrange
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        UpdateOrderHandler handler = new(repository, unitOfWork);

        Guid id = Guid.NewGuid();
        repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((ChangeOrderEntity?)null);

        UpdateOrderCommand command = BuildCommand(id);

        // Act
        Result<Guid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(DomainErrors.Order.NotFound.Code);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Cuando SaveChangesAsync lanza ConcurrencyException, retorna ConcurrencyConflict.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ConcurrencyConflict_ReturnsConcurrencyError()
    {
        // Arrange
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        UpdateOrderHandler handler = new(repository, unitOfWork);

        ChangeOrderEntity order = OrderBuilder.Build();
        UpdateOrderCommand command = BuildCommand(order.Id);

        repository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>())
            .Returns(order);

        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConcurrencyException());

        // Act
        Result<Guid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(DomainErrors.Order.ConcurrencyConflict.Code);
    }
}
