using ChangeOrder.Business.Queries.GetAllOrders;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Errors;
using FluentAssertions;
using NSubstitute;

namespace ChangeOrder.Business.Tests.Queries;

/// <summary>
/// Tests unitarios para <see cref="GetAllOrdersHandler"/>.
/// </summary>
public sealed class GetAllOrdersHandlerTests
{
    /// <summary>
    /// Cuando existen órdenes, retorna todas en la lista del resultado.
    /// </summary>
    [Fact]
    public async Task HandleAsync_OrdersExist_ReturnsAllOrders()
    {
        // Arrange
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        GetAllOrdersHandler handler = new(repository);

        IReadOnlyList<ChangeOrderEntity> orders = new List<ChangeOrderEntity>
        {
            OrderBuilder.Build(),
            OrderBuilder.Build()
        };

        repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(orders);

        GetAllOrdersQuery query = new(Page: 1, PageSize: 10);

        // Act
        Result<IReadOnlyList<ChangeOrderEntity>, Error> result =
            await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().BeEquivalentTo(orders);
    }

    /// <summary>
    /// Cuando no hay órdenes, retorna éxito con lista vacía.
    /// </summary>
    [Fact]
    public async Task HandleAsync_NoOrders_ReturnsEmptyList()
    {
        // Arrange
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        GetAllOrdersHandler handler = new(repository);

        repository.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ChangeOrderEntity>());

        GetAllOrdersQuery query = new(Page: 1, PageSize: 10);

        // Act
        Result<IReadOnlyList<ChangeOrderEntity>, Error> result =
            await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
