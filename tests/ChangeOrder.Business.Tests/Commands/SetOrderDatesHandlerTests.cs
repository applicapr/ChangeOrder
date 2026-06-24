using ChangeOrder.Business.Commands.SetOrderDates;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Enums;
using ChangeOrder.Domain.Errors;
using FluentAssertions;
using NSubstitute;

namespace ChangeOrder.Business.Tests.Commands;

/// <summary>
/// Tests unitarios para <see cref="SetOrderDatesHandler"/>.
/// </summary>
public sealed class SetOrderDatesHandlerTests
{
    /// <summary>
    /// Cuando se establece DeliveryDate, el estado cambia a InProgress.
    /// </summary>
    [Fact]
    public async Task HandleAsync_OrderExists_SetsDeliveryDate_StatusChangesToInProgress()
    {
        // Arrange
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        SetOrderDatesHandler handler = new(repository, unitOfWork);

        ChangeOrderEntity order = OrderBuilder.Build();
        DateTime deliveryDate = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        repository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>())
            .Returns(order);

        SetOrderDatesCommand command = new(
            Id: order.Id,
            DeliveryDate: deliveryDate,
            InitialEvaluationDate: null,
            ProductionDeployDate: null
        );

        // Act
        Result<Guid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(order.Id);
        order.DeliveryDate.Should().Be(deliveryDate);
        order.Status.Should().Be(OrderStatus.InProgress);
        repository.Received(1).Update(order);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Cuando se establece ProductionDeployDate, el estado cambia a Deployed.
    /// </summary>
    [Fact]
    public async Task HandleAsync_OrderExists_SetsProductionDeployDate_StatusChangesToDeployed()
    {
        // Arrange
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        SetOrderDatesHandler handler = new(repository, unitOfWork);

        ChangeOrderEntity order = OrderBuilder.Build();
        DateTime deployDate = new(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);

        repository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>())
            .Returns(order);

        SetOrderDatesCommand command = new(
            Id: order.Id,
            DeliveryDate: null,
            InitialEvaluationDate: null,
            ProductionDeployDate: deployDate
        );

        // Act
        Result<Guid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        order.ProductionDeployDate.Should().Be(deployDate);
        order.Status.Should().Be(OrderStatus.Deployed);
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
        SetOrderDatesHandler handler = new(repository, unitOfWork);

        Guid id = Guid.NewGuid();
        repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((ChangeOrderEntity?)null);

        SetOrderDatesCommand command = new(
            Id: id,
            DeliveryDate: DateTime.UtcNow,
            InitialEvaluationDate: null,
            ProductionDeployDate: null
        );

        // Act
        Result<Guid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(DomainErrors.Order.NotFound.Code);
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
