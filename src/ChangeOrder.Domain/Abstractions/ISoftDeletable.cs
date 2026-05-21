namespace ChangeOrder.Domain.Abstractions;

/// <summary>
/// Contrato para entidades con borrado lógico — nunca se eliminan físicamente de la base de datos
/// </summary>
public interface ISoftDeletable
{
    /// <summary>
    /// Flag de borrado logico — DEFAULT false ꞏ HasQueryFilter filtra automaticamente
    /// </summary>

    public bool IsDeleted { get; set; }

    /// <summary>
    /// Timestamp del borrado logico — set automaticamente por AuditInterceptor
    /// </summary>
    public DateTime? DeletedAt { get; set; }
}
