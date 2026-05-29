using ChangeOrder.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ChangeOrder.Data.Interceptors;

/// <summary>
/// Interceptor de EF Core que gestiona auditoría y soft-delete automáticamente antes de cada SaveChangesAsync.
/// </summary>

public sealed class AuditInterceptor : SaveChangesInterceptor
{
    /// <summary>
    /// Intercepta el guardado de cambios para actualizar timestamps de auditoría y convertir eliminaciones físicas en borrado lógico.
    /// </summary>

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
    DbContextEventData eventData,
    InterceptionResult<int> result,
    CancellationToken cancellationToken = default)
    {
        DbContext? context = eventData.Context;
        if (context is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        DateTime now = DateTime.UtcNow;

        // Auditoria automatica — actualiza UpdatedAt
        foreach (EntityEntry<IAuditable> entry in
        context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = now;
        }

        // Soft Delete — convierte Delete en Modified
        foreach (EntityEntry<ISoftDeletable> entry in
        context.ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State != EntityState.Deleted)
                continue;
            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = now;
        }
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
