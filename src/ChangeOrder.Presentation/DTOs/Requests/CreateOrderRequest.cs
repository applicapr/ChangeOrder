namespace ChangeOrder.Presentation.DTOs.Requests;

/// <summary>
/// Request body of <c>POST /api/v1/change-orders</c>. Mirrors the
/// <c>CreateOrderRequest</c> schema defined in <c>contracts/openapi.yaml</c>.
/// </summary>
/// <param name="ProgramName">Subject application name.</param>
/// <param name="ProductionVersion">Pre-change production version.</param>
/// <param name="VersionScreenshotPath">Optional pre-change evidence path.</param>
/// <param name="WorkDescription">Short description.</param>
/// <param name="RequestDetails">Detailed specification.</param>
/// <param name="Justification">Business justification.</param>
/// <param name="RequiredAction">Required action description.</param>
/// <param name="Requester">Requester contact information.</param>
public sealed record CreateOrderRequest(
    string ProgramName,
    string ProductionVersion,
    string? VersionScreenshotPath,
    string WorkDescription,
    string RequestDetails,
    string Justification,
    string RequiredAction,
    RequesterInfoDto Requester);
