namespace ChangeOrder.Domain.Abstractions;

/// <summary>
/// Marks an entity tracked for creation and last-modification timestamps.
/// Values are populated by the <c>AuditInterceptor</c> (Constitution Principle IV);
/// handlers MUST NOT set them manually.
/// </summary>
public interface IAuditable
{
    /// <summary>UTC instant at which the row was first persisted.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC instant of the last modification. <c>null</c> on rows that have never been updated.</summary>
    public DateTime? UpdatedAt { get; set; }
}
