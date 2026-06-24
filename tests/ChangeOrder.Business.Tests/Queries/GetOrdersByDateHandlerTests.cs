using ChangeOrder.Business.Queries.GetOrdersByDate;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Errors;
using FluentAssertions;
using NSubstitute;

namespace ChangeOrder.Business.Tests.Queries;

/// <summary>
/// Tests unitarios para <see cref="GetOrdersByDateHandler"/>.
/// </summary>
public sealed class GetOrdersByDateHandlerTests
{
    /// <summary>
    /// Cuando existen órdenes para la fecha dada, retorna la lista con esas órdenes.
    /// </summary>
    [Fact]
    public async Task HandleAsync_OrdersExistForDate_ReturnsOrders()
    {
        // Arrange
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        GetOrdersByDateHandler handler = new(repository);

        DateTime date = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        IReadOnlyList<ChangeOrderEntity> orders = new List<ChangeOrderEntity>
        {
            OrderBuilder.Build(),
            OrderBuilder.Build()
        };

        repository.GetByDateAsync(date, Arg.Any<CancellationToken>())
            .Returns(orders);

        GetOrdersByDateQuery query = new(date);

        // Act
        Result<IReadOnlyList<ChangeOrderEntity>, Error> result =
            await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().BeEquivalentTo(orders);
    }

    /// <summary>
    /// Cuando no hay órdenes para la fecha dada, retorna éxito con lista vacía.
    /// </summary>
    [Fact]
    public async Task HandleAsync_NoOrdersForDate_ReturnsEmptyList()
    {
        // Arrange
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        GetOrdersByDateHandler handler = new(repository);

        DateTime date = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        repository.GetByDateAsync(date, Arg.Any<CancellationToken>())
            .Returns(new List<ChangeOrderEntity>());

        GetOrdersByDateQuery query = new(date);

        // Act
        Result<IReadOnlyList<ChangeOrderEntity>, Error> result =
            await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
