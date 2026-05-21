namespace ChangeOrder.Domain.Enums;

/// <summary>
/// Representa el estado de ciclo de vida completo de una orden de cambio
/// </summary>
public enum OrderStatus
{
    /// <summary>
    /// Borrador - recien creada, no enviada a aprobacion
    /// </summary>
    Draft,

    /// <summary>
    /// Pendiente a aprovacion por la cadena de 4 niveles
    /// </summary>
    PendingApproval,

    /// <summary>
    /// Aprovada completamente - Lista para iniciar el trabajo
    /// </summary>
    Approved,

    /// <summary>
    /// Se esta trabajando con el cambio
    /// </summary>
    InProgress,

    /// <summary>
    /// Cambio desplegado exitosamente en produccion
    /// </summary>
    Deployed,

    /// <summary>
    /// Cancelada - por rechazo o decision tomada 
    /// </summary>
    Canceled
}
