namespace ChangeOrder.Presentation.DTOs.Responses;

/// <summary>
/// Inline JSON object emitted under <c>requester</c> in
/// <c>OrderResponse</c>. Matches the <c>RequesterInfo</c> schema defined in
/// <c>contracts/openapi.yaml</c>.
/// </summary>
/// <param name="Name">Full name.</param>
/// <param name="Position">Job title.</param>
/// <param name="Department">Department.</param>
/// <param name="Email">Contact e-mail.</param>
public sealed record RequesterInfoResponse(
    string Name,
    string Position,
    string Department,
    string Email);
