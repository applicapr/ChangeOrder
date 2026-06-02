using ChangeOrder.Domain.ValueObjects;
using ChangeOrder.Domain.Enums;
using ChangeOrder.Domain.Abstractions;

namespace ChangeOrder.Domain.Entities;

/// <summary>
/// Entidad principal que representa una solicitud de cambio completa
/// Aggregate Root del dominio ChangeOrder
/// </summary>
public class ChangeOrderEntity : IAuditable, ISoftDeletable
{
    /// <summary>
    /// Identificador único de la orden generado en la aplicación
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Número de orden con formato yyyyMMdd-##. Ejemplo: 20260224-01
    /// </summary>
    public required OrderNumber Number { get; init; }

    /// <summary>
    /// Nombre del programa o módulo a cambiar
    /// </summary>
    public required string ProgramName { get; set; }

    /// <summary>
    /// Versión en producción antes del cambio
    /// </summary>
    public required string ProductionVersion { get; set; }

    /// <summary>
    /// Ruta al screenshot de la versión antes del cambio. Opcional
    /// </summary>
    public string? VersionScreenshotPath { get; set; }

    /// <summary>
    /// Fecha en que se solicita el cambio
    /// </summary>
    public required DateTime RequestDate { get; init; }

    /// <summary>
    /// Descripción breve del trabajo solicitado
    /// </summary>
    public required string WorkDescription { get; set; }

    /// <summary>
    /// Detalle completo de la solicitud
    /// </summary>
    public required string RequestDetails { get; set; }

    /// <summary>
    /// Justificación del negocio para el cambio
    /// </summary>
    public required string Justification { get; set; }

    /// <summary>
    /// Acción requerida al programador
    /// </summary>
    public required string RequiredAction { get; set; }

    /// <summary>
    /// Información del solicitante — nombre, cargo, departamento y email
    /// </summary>
    public required RequesterInfo Requester { get; init; }

    /// <summary>
    /// Cadena de aprobación en 4 niveles jerárquicos
    /// </summary>
    public required ApprovalChain Approval { get; init; }

    /// <summary>
    /// Estado del ciclo de vida de la orden
    /// </summary>
    public required OrderStatus Status { get; set; }

    /// <summary>
    /// Fecha estimada de entrega. Opcional
    /// </summary>
    public DateTime? DeliveryDate { get; set; }

    /// <summary>
    /// Fecha de primera evaluación técnica. Opcional
    /// </summary>
    public DateTime? InitialEvaluationDate { get; set; }

    /// <summary>
    /// Fecha real de despliegue en producción. Opcional
    /// </summary>
    public DateTime? ProductionDeployDate { get; set; }

    /// <summary>
    /// Ruta al screenshot post-cambio. Opcional
    /// </summary>
    public string? PostDeployScreenshotPath { get; set; }

    /// <summary>
    /// Timestamp de creación en UTC — asignado automáticamente por el AuditInterceptor
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp de última modificación en UTC — actualizado automáticamente por el AuditInterceptor
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Indica si la entidad fue borrada lógicamente. Nunca se elimina físicamente
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Timestamp del borrado lógico en UTC — asignado automáticamente por el AuditInterceptor
    /// </summary>
    public DateTime? DeletedAt { get; set; }
}
