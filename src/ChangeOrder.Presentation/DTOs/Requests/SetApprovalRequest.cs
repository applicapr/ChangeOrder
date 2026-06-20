namespace ChangeOrder.Presentation.DTOs.Requests;

/// <summary>
/// Datos que el cliente envía para registrar un veredicto de aprobación
/// </summary>
public sealed record SetApprovalRequest(string Verdict);
