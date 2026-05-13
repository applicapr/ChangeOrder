namespace ChangeOrder.Presentation.DTOs.Requests;

/// <summary>
/// Request body of <c>PUT /api/v1/change-orders/{id}</c>. Matches the
/// <c>UpdateOrderRequest</c> schema in <c>contracts/openapi.yaml</c>: same
/// shape as <c>CreateOrderRequest</c> plus a mandatory base64-encoded
/// <see cref="RowVersion"/> token used to enforce optimistic concurrency
/// (FR-013).
/// </summary>
/// <param name="ProgramName">Subject application name.</param>
/// <param name="ProductionVersion">Pre-change production version.</param>
/// <param name="VersionScreenshotPath">Optional pre-change evidence path.</param>
/// <param name="WorkDescription">Short description.</param>
/// <param name="RequestDetails">Detailed specification.</param>
/// <param name="Justification">Business justification.</param>
/// <param name="RequiredAction">Required action description.</param>
/// <param name="Requester">Requester contact information.</param>
/// <param name="RowVersion">Base64-encoded SQL Server <c>rowversion</c> previously received in a GET.</param>
public sealed record UpdateOrderRequest(
    string ProgramName,
    string ProductionVersion,
    string? VersionScreenshotPath,
    string WorkDescription,
    string RequestDetails,
    string Justification,
    string RequiredAction,
    RequesterInfoDto Requester,
    string RowVersion);
