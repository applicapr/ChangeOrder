using ChangeOrder.Business.Commands.DeleteOrder;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Errors;
using FluentAssertions;
using NSubstitute;

namespace ChangeOrder.Business.Tests.Commands;

/// <summary>
/// Tests unitarios para <see cref="DeleteOrderHandler"/>.
/// </summary>
public sealed class DeleteOrderHandlerTests
{
    /// <summary>
    /// Cuando la orden existe, la elimina lógicamente y retorna el Id.
    /// </summary>
    [Fact]
    public async Task HandleAsync_OrderExists_DeletesOrderAndReturnsId()
    {
        // Arrange
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        DeleteOrderHandler handler = new(repository, unitOfWork);

        ChangeOrderEntity order = OrderBuilder.Build();

        repository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>())
            .Returns(order);

        DeleteOrderCommand command = new(order.Id);

        // Act
        Result<Guid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(order.Id);
        repository.Received(1).Delete(order);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Cuando la orden no existe, retorna NotFound sin llamar a Delete ni SaveChanges.
    /// </summary>
    [Fact]
    public async Task HandleAsync_OrderNotFound_ReturnsNotFoundError()
    {
        // Arrange
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        DeleteOrderHandler handler = new(repository, unitOfWork);

        Guid id = Guid.NewGuid();
        repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((ChangeOrderEntity?)null);

        DeleteOrderCommand command = new(id);

        // Act
        Result<Guid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(DomainErrors.Order.NotFound.Code);
        repository.DidNotReceive().Delete(Arg.Any<ChangeOrderEntity>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
