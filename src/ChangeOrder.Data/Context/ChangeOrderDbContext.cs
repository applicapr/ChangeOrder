using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ChangeOrder.Data.Context;

/// <summary>
/// DbContext principal de EF Core. Representa la conexión con la base de datos
/// e implementa IUnitOfWork para gestionar transacciones atómicas  
/// </summary>

public sealed class ChangeOrderDbContext(DbContextOptions<ChangeOrderDbContext>
options)
 : DbContext(options), IUnitOfWork
{
    /// <summary>
    /// Tabla principal de órdenes de cambio
    /// </summary>

    public DbSet<ChangeOrderEntity> ChangeOrders => Set<ChangeOrderEntity>();

    /// <summary>
    /// Tabla de registros de idempotencia para prevenir duplicados en POST.
    /// </summary>

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    /// <summary>
    /// Aplica todas las configuraciones de entidades del assembly automáticamente.
    /// </summary>

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChangeOrderDbContext).Assembly);
    }

    /// <summary>
    /// Guarda los cambios pendientes. Traduce DbUpdateConcurrencyException de EF Core
    /// a ConcurrencyException del dominio, para que Business no dependa de EF Core.
    /// </summary>

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ChangeOrder.Domain.Errors.ConcurrencyException();
        }
    }
}
