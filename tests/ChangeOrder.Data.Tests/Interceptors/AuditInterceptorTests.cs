using ChangeOrder.Data.Context;
using ChangeOrder.Data.Tests.Helpers;
using ChangeOrder.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ChangeOrder.Data.Tests.Interceptors;

/// <summary>
/// Tests de integración para <see cref="ChangeOrder.Data.Interceptors.AuditInterceptor"/>.
/// Verifica que los timestamps de auditoría y los campos de soft-delete se gestionan automáticamente.
/// </summary>
public sealed class AuditInterceptorTests
{
    /// <summary>
    /// Al guardar una entidad en estado Modified, el interceptor asigna UpdatedAt automáticamente.
    /// </summary>
    [Fact]
    public async Task SaveChanges_ModifiedEntity_UpdatesUpdatedAt()
    {
        // Arrange
        await using ChangeOrderDbContext context = DbContextFactory.CreateWithAudit();
        ChangeOrderEntity order = OrderBuilder.Build();

        await context.ChangeOrders.AddAsync(order);
        await context.SaveChangesAsync(); // Added → interceptor no toca UpdatedAt

        order.UpdatedAt.Should().BeNull();

        // Act — modificar una propiedad; EF detectará el cambio como Modified en el próximo save
        order.ProgramName = "Programa Modificado";
        await context.SaveChangesAsync(); // Modified → interceptor asigna UpdatedAt

        // Assert
        order.UpdatedAt.Should().NotBeNull();
        order.UpdatedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Al eliminar una entidad (Remove), el interceptor convierte el delete en soft-delete:
    /// pone IsDeleted=true y asigna DeletedAt, dejando el registro físico en la BD.
    /// </summary>
    [Fact]
    public async Task SaveChanges_DeletedEntity_SetsSoftDeleteFields()
    {
        // Arrange
        await using ChangeOrderDbContext context = DbContextFactory.CreateWithAudit();
        ChangeOrderEntity order = OrderBuilder.Build();

        await context.ChangeOrders.AddAsync(order);
        await context.SaveChangesAsync();

        // Act
        context.ChangeOrders.Remove(order); // Estado: Deleted
        await context.SaveChangesAsync();   // Interceptor convierte a Modified + IsDeleted=true

        // Assert — IgnoreQueryFilters para ver el registro que el filtro normalmente ocultaría
        ChangeOrderEntity? result = await context.ChangeOrders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == order.Id);

        result.Should().NotBeNull();
        result!.IsDeleted.Should().BeTrue();
        result.DeletedAt.Should().NotBeNull();
        result.DeletedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
