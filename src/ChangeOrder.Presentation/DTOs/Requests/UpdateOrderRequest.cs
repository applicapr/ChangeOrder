namespace ChangeOrder.Presentation.DTOs.Requests;

/// <summary>
/// Datos que el cliente envía para actualizar una orden de cambio existente
/// </summary>

public sealed record UpdateOrderRequest(

    Guid Id,
    string ProgramName,
    string ProductionVersion,
    string? VersionScreenshotPath,
    string WorkDescription,
    string RequestDetails,
    string Justification,
    string RequiredAction,
    DateTime? DeliveryDate,
    string Status,
    byte[] RowVersion

);
