namespace ChangeOrder.Presentation.DTOs.Responses;

/// <summary>
/// Respuesta con el resumen de una orden de cambio
/// </summary>

public sealed record OrderResponse(
    Guid Id,
    string OrderNumber,
    string ProgramName,
    DateTime RequestDate,
    string RequesterName,
    string Status,
    byte[] RowVersion
);
