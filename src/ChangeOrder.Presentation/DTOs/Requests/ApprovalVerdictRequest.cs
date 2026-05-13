namespace ChangeOrder.Presentation.DTOs.Requests;

/// <summary>
/// Request body of <c>PUT /api/v1/change-orders/{id}/approvals/{level}</c>.
/// Matches the <c>ApprovalVerdictRequest</c> schema in
/// <c>contracts/openapi.yaml</c>: a single <c>verdict</c> field whose value
/// is one of <c>Pending | Approved | Rejected</c>.
/// </summary>
/// <param name="Verdict">Verdict to record on the addressed approval slot.</param>
public sealed record ApprovalVerdictRequest(string Verdict);
