namespace ChangeOrder.Domain.Enums;

/// <summary>
/// Representa el estado de una aprovacion individual dentro de la cadena de aprobacion
/// </summary>

public enum ApprovalStatus
{
    /// <summary>
    /// Esperando desicion - Estado incial
    /// </summary>
    Pending,

    /// <summary>
    /// Aprovado por el responsable del nivel
    /// </summary>
    Approved,

    /// <summary>
    /// Rechazado - la orden puede ser cancelada o revisada
    /// </summary>
    Rejected
}
