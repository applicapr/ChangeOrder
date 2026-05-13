namespace ChangeOrder.Presentation.DTOs.Responses;

/// <summary>
/// Inline JSON object emitted under <c>approvalChain</c> in
/// <c>OrderResponse</c>. Matches the <c>ApprovalChain</c> schema defined in
/// <c>contracts/openapi.yaml</c>.
/// </summary>
/// <param name="RequesterApproval">Verdict from the requester.</param>
/// <param name="DepartmentHeadApproval">Verdict from the department head.</param>
/// <param name="ItHeadApproval">Verdict from the IT head.</param>
/// <param name="ProgrammingDivisionApproval">Verdict from the programming division.</param>
public sealed record ApprovalChainResponse(
    string RequesterApproval,
    string DepartmentHeadApproval,
    string ItHeadApproval,
    string ProgrammingDivisionApproval);
