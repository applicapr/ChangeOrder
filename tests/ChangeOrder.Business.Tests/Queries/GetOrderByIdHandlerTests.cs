using ChangeOrder.Business.Queries.GetOrderById;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Errors;
using FluentAssertions;
using NSubstitute;

namespace ChangeOrder.Business.Tests.Queries;

/// <summary>
/// Tests unitarios para <see cref="GetOrderByIdHandler"/>.
/// </summary>
public sealed class GetOrderByIdHandlerTests
{
    /// <summary>
    /// Cuando la orden existe, retorna la entidad en el Value del resultado.
    /// </summary>
    [Fact]
    public async Task HandleAsync_OrderExists_ReturnsOrder()
    {
        // Arrange
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        GetOrderByIdHandler handler = new(repository);

        ChangeOrderEntity order = OrderBuilder.Build();

        repository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>())
            .Returns(order);

        GetOrderByIdQuery query = new(order.Id);

        // Act
        Result<ChangeOrderEntity, Error> result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(order);
        result.Value!.Id.Should().Be(order.Id);
    }

    /// <summary>
    /// Cuando la orden no existe, retorna fallo con el error NotFound.
    /// </summary>
    [Fact]
    public async Task HandleAsync_OrderNotFound_ReturnsFailure()
    {
        // Arrange
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        GetOrderByIdHandler handler = new(repository);

        Guid id = Guid.NewGuid();
        repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((ChangeOrderEntity?)null);

        GetOrderByIdQuery query = new(id);

        // Act
        Result<ChangeOrderEntity, Error> result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(DomainErrors.Order.NotFound.Code);
        result.Value.Should().BeNull();
    }
}
