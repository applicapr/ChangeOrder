using ChangeOrder.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ChangeOrder.Data.Interceptors;

/// <summary>
/// Single writer of audit and soft-delete columns (Constitution Principle IV).
/// Handlers MUST NOT touch <c>CreatedAt</c>, <c>UpdatedAt</c>, <c>IsDeleted</c>
/// or <c>DeletedAt</c> directly; the interceptor walks the ChangeTracker on
/// every <c>SaveChangesAsync</c> and applies the rules.
/// </summary>
public sealed class AuditInterceptor : SaveChangesInterceptor
{
    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is not null)
        {
            ApplyAudit(eventData.Context);
        }
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is not null)
        {
            ApplyAudit(eventData.Context);
        }
        return base.SavingChanges(eventData, result);
    }

    private static void ApplyAudit(DbContext context)
    {
        DateTime nowUtc = DateTime.UtcNow;
        foreach (EntityEntry entry in context.ChangeTracker.Entries())
        {
            ApplyAuditRules(entry, nowUtc);
            ApplySoftDeleteRules(entry, nowUtc);
        }
    }

    private static void ApplyAuditRules(EntityEntry entry, DateTime nowUtc)
    {
        if (entry.Entity is not IAuditable auditable)
        {
            return;
        }

        if (entry.State == EntityState.Added)
        {
            auditable.CreatedAt = nowUtc;
            return;
        }

        if (entry.State == EntityState.Modified)
        {
            auditable.UpdatedAt = nowUtc;
        }
    }

    private static void ApplySoftDeleteRules(EntityEntry entry, DateTime nowUtc)
    {
        if (entry.State != EntityState.Deleted || entry.Entity is not ISoftDeletable softDeletable)
        {
            return;
        }

        entry.State = EntityState.Modified;
        softDeletable.IsDeleted = true;
        softDeletable.DeletedAt = nowUtc;
        if (entry.Entity is IAuditable auditable)
        {
            auditable.UpdatedAt = nowUtc;
        }

        // EF detaches owned entries when the aggregate is set to Deleted.
        // Flipping the parent back to Modified does not restore them, so the
        // generated UPDATE would send NULL for every owned column and SQL
        // Server would reject it. Re-tracking the owned references as
        // Unchanged keeps their stored values in the UPDATE statement.
        foreach (ReferenceEntry reference in entry.References)
        {
            if (reference.TargetEntry is { } targetEntry && targetEntry.Metadata.IsOwned())
            {
                targetEntry.State = EntityState.Unchanged;
            }
        }
    }
}
