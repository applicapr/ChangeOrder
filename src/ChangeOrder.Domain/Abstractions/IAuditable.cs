namespace ChangeOrder.Domain.Abstractions;

/// <summary>
/// Contrato para entidades auditable con timestamps de creacion y actualizacion
/// </summary>

public interface IAuditable
{
    /// <summary>
    /// Timestamp de creacion en UTC, no modificable después de la creacion
    /// </summary>
    public DateTime CreatedAt { get;  }

    /// <summary>
    /// Timestamp de ulitma actualización en UTC, se actualiza automáticamente en cada cambio
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
