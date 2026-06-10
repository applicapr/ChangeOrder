namespace ChangeOrder.Presentation.DTOs.Requests;

/// <summary>
/// Datos que el cliente envía para crear una nueva orden de cambio
/// </summary>

public sealed record CreateOrderRequest(
    Guid IdempotencyKey,
    string ProgramName,
    string ProductionVersion,
    string? VersionScreenshotPath,
    DateTime RequestDate,
    string WorkDescription,
    string RequestDetails,
    string Justification,
    string RequiredAction,
    string RequesterName,
    string RequesterPosition,
    string RequesterDepartment,
    string RequesterEmail
);
