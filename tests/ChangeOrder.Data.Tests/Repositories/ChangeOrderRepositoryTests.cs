using ChangeOrder.Data.Context;
using ChangeOrder.Data.Repositories;
using ChangeOrder.Data.Tests.Helpers;
using ChangeOrder.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ChangeOrder.Data.Tests.Repositories;

/// <summary>
/// Tests de integración para <see cref="ChangeOrderRepository"/> usando base de datos InMemory.
/// Cada test crea su propia instancia de base de datos aislada.
/// </summary>
public sealed class ChangeOrderRepositoryTests
{
    /// <summary>
    /// Cuando la orden existe, GetByIdAsync la retorna correctamente.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_OrderExists_ReturnsOrder()
    {
        // Arrange
        await using ChangeOrderDbContext context = DbContextFactory.CreateWithAudit();
        ChangeOrderRepository repository = new(context);
        ChangeOrderEntity order = OrderBuilder.Build();

        await repository.AddAsync(order, CancellationToken.None);
        await context.SaveChangesAsync();

        // Act
        ChangeOrderEntity? result = await repository.GetByIdAsync(order.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(order.Id);
        result.ProgramName.Should().Be("TestApp");
    }

    /// <summary>
    /// Cuando la orden no existe, GetByIdAsync retorna null.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_OrderDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using ChangeOrderDbContext context = DbContextFactory.CreateWithAudit();
        ChangeOrderRepository repository = new(context);

        // Act
        ChangeOrderEntity? result = await repository.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Cuando la orden está soft-deleted, el HasQueryFilter la excluye y GetByIdAsync retorna null.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_OrderIsSoftDeleted_ReturnsNull()
    {
        // Arrange
        await using ChangeOrderDbContext context = DbContextFactory.CreateWithAudit();
        ChangeOrderRepository repository = new(context);

        ChangeOrderEntity order = OrderBuilder.Build();
        order.IsDeleted = true;

        // Agregamos directamente para persistir IsDeleted=true sin pasar por el interceptor
        await context.ChangeOrders.AddAsync(order);
        await context.SaveChangesAsync();

        // Act
        ChangeOrderEntity? result = await repository.GetByIdAsync(order.Id, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// GetAllAsync excluye las órdenes soft-deleted y retorna sólo las activas.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_ReturnsOnlyNonDeleted()
    {
        // Arrange
        await using ChangeOrderDbContext context = DbContextFactory.CreateWithAudit();
        ChangeOrderRepository repository = new(context);

        ChangeOrderEntity active = OrderBuilder.Build(1);
        ChangeOrderEntity deleted = OrderBuilder.Build(2);
        deleted.IsDeleted = true;

        await context.ChangeOrders.AddRangeAsync(active, deleted);
        await context.SaveChangesAsync();

        // Act
        IReadOnlyList<ChangeOrderEntity> result = await repository.GetAllAsync(CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be(active.Id);
    }

    /// <summary>
    /// GetByDateAsync retorna sólo las órdenes cuyo RequestDate coincide con la fecha solicitada.
    /// </summary>
    [Fact]
    public async Task GetByDateAsync_ReturnsOrdersForThatDate()
    {
        // Arrange
        await using ChangeOrderDbContext context = DbContextFactory.CreateWithAudit();
        ChangeOrderRepository repository = new(context);

        DateTime targetDate = new(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);
        DateTime otherDate = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

        ChangeOrderEntity match1 = OrderBuilder.Build(1, targetDate);
        ChangeOrderEntity match2 = OrderBuilder.Build(2, targetDate);
        ChangeOrderEntity noMatch = OrderBuilder.Build(3, otherDate);

        await context.ChangeOrders.AddRangeAsync(match1, match2, noMatch);
        await context.SaveChangesAsync();

        // Act
        IReadOnlyList<ChangeOrderEntity> result =
            await repository.GetByDateAsync(targetDate, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Select(o => o.Id).Should().BeEquivalentTo(new[] { match1.Id, match2.Id });
    }

    /// <summary>
    /// Con base de datos vacía, GetNextSequenceForDateAsync retorna 1.
    /// </summary>
    [Fact]
    public async Task GetNextSequenceForDateAsync_NoOrders_Returns1()
    {
        // Arrange
        await using ChangeOrderDbContext context = DbContextFactory.CreateWithAudit();
        ChangeOrderRepository repository = new(context);
        DateTime date = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        int result = await repository.GetNextSequenceForDateAsync(date, CancellationToken.None);

        // Assert
        result.Should().Be(1);
    }

    /// <summary>
    /// Con una orden ya existente para la fecha, GetNextSequenceForDateAsync retorna 2.
    /// </summary>
    [Fact]
    public async Task GetNextSequenceForDateAsync_OneOrderExists_Returns2()
    {
        // Arrange
        await using ChangeOrderDbContext context = DbContextFactory.CreateWithAudit();
        ChangeOrderRepository repository = new(context);
        DateTime date = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        await repository.AddAsync(OrderBuilder.Build(1, date), CancellationToken.None);
        await context.SaveChangesAsync();

        // Act
        int result = await repository.GetNextSequenceForDateAsync(date, CancellationToken.None);

        // Assert
        result.Should().Be(2);
    }

    /// <summary>
    /// AddAsync + SaveChangesAsync persiste la orden en la base de datos.
    /// </summary>
    [Fact]
    public async Task AddAsync_SaveChanges_PersistsOrder()
    {
        // Arrange
        await using ChangeOrderDbContext context = DbContextFactory.CreateWithAudit();
        ChangeOrderRepository repository = new(context);
        ChangeOrderEntity order = OrderBuilder.Build();

        // Act
        await repository.AddAsync(order, CancellationToken.None);
        await context.SaveChangesAsync();

        // Assert — consultamos con el mismo contexto para confirmar que está en el store
        ChangeOrderEntity? persisted = await repository.GetByIdAsync(order.Id, CancellationToken.None);
        persisted.Should().NotBeNull();
        persisted!.Id.Should().Be(order.Id);
        persisted.ProgramName.Should().Be(order.ProgramName);
    }

    /// <summary>
    /// Delete + SaveChangesAsync aplica borrado lógico: el registro persiste en la BD
    /// con IsDeleted=true y las consultas normales ya no lo devuelven.
    /// </summary>
    [Fact]
    public async Task Delete_SaveChanges_SoftDeletesOrder()
    {
        // Arrange
        await using ChangeOrderDbContext context = DbContextFactory.CreateWithAudit();
        ChangeOrderRepository repository = new(context);
        ChangeOrderEntity order = OrderBuilder.Build();

        await repository.AddAsync(order, CancellationToken.None);
        await context.SaveChangesAsync();

        // Act
        repository.Delete(order);
        await context.SaveChangesAsync();

        // Assert — el query filter la excluye
        ChangeOrderEntity? notFound = await repository.GetByIdAsync(order.Id, CancellationToken.None);
        notFound.Should().BeNull();

        // El registro sigue en la BD con IsDeleted=true
        ChangeOrderEntity? softDeleted = await context.ChangeOrders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == order.Id);

        softDeleted.Should().NotBeNull();
        softDeleted!.IsDeleted.Should().BeTrue();
        softDeleted.DeletedAt.Should().NotBeNull();
    }
}
