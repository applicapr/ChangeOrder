namespace ChangeOrder.Domain.Abstractions;

/// <summary>
/// Marks an entity that is logically removed via a flag instead of a physical
/// <c>DELETE</c> statement. Setting <see cref="IsDeleted"/> and
/// <see cref="DeletedAt"/> is the responsibility of the <c>AuditInterceptor</c>
/// (Constitution Principle IV).
/// </summary>
public interface ISoftDeletable
{
    /// <summary>Logical-deletion flag. Default-excluded by the global query filter.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>UTC instant at which the row was soft-deleted.</summary>
    public DateTime? DeletedAt { get; set; }
}
