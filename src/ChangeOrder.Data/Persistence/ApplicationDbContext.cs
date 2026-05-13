using ChangeOrder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using DomainChangeOrder = ChangeOrder.Domain.Entities.ChangeOrder;

namespace ChangeOrder.Data.Persistence;

/// <summary>
/// EF Core DbContext for the ChangeOrder solution. Owns the
/// <see cref="ChangeOrders"/> and <see cref="IdempotencyKeys"/> sets, applies
/// the configuration types in this assembly and installs the soft-delete
/// global query filter.
/// </summary>
public sealed class ApplicationDbContext : DbContext
{
    /// <summary>All non-soft-deleted change orders (filter applied via fluent config).</summary>
    public DbSet<DomainChangeOrder> ChangeOrders => Set<DomainChangeOrder>();

    /// <summary>Active idempotency rows (NOT soft-deletable).</summary>
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    /// <summary>Required by EF Core tooling.</summary>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
