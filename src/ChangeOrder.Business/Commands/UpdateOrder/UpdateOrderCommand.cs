using ChangeOrder.Domain.Enums;

namespace ChangeOrder.Business.Commands.UpdateOrder;

/// <summary>
/// Command para actualizar los campos modificables de una orden de cambio existente
/// </summary>

public sealed record UpdateOrderCommand(
    Guid Id,
    string ProgramName,
    string ProductionVersion,
    string? VersionScreenshotPath,
    string WorkDescription,
    string RequestDetails,
    string Justification,
    string RequiredAction,
    DateTime? DeliveryDate,
    OrderStatus Status
);
