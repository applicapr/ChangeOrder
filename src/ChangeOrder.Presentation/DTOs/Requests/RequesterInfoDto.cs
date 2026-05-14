namespace ChangeOrder.Presentation.DTOs.Requests;

/// <summary>
/// Inline JSON object received under <c>requester</c> on
/// <c>POST /api/v1/change-orders</c>. Matches the <c>RequesterInfo</c> schema
/// defined in <c>contracts/openapi.yaml</c>.
/// </summary>
/// <param name="Name">Full name (≤ 150 chars).</param>
/// <param name="Position">Job title (≤ 100 chars).</param>
/// <param name="Department">Department (≤ 100 chars).</param>
/// <param name="Email">Contact e-mail (RFC-5322, ≤ 200 chars).</param>
public sealed record RequesterInfoDto(
    string Name,
    string Position,
    string Department,
    string Email);
