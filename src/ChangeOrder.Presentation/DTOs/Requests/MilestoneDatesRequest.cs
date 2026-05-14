namespace ChangeOrder.Presentation.DTOs.Requests;

/// <summary>
/// Request body of <c>PATCH /api/v1/change-orders/{id}/dates</c>. Every field
/// is optional — only the supplied ones are applied. Matches the
/// <c>MilestoneDatesRequest</c> schema in <c>contracts/openapi.yaml</c>.
/// </summary>
/// <param name="DeliveryDate">Drives Approved → InProgress when supplied.</param>
/// <param name="InitialEvaluationDate">Recorded without changing workflow status.</param>
/// <param name="ProductionDeployDate">Drives InProgress → Deployed when supplied.</param>
/// <param name="PostDeployScreenshotPath">Optional post-deploy evidence path.</param>
public sealed record MilestoneDatesRequest(
    DateTime? DeliveryDate,
    DateTime? InitialEvaluationDate,
    DateTime? ProductionDeployDate,
    string? PostDeployScreenshotPath);
