namespace ChangeOrder.Business.Commands.RecordMilestoneDates;

/// <summary>
/// CQRS command for <c>PATCH /api/v1/change-orders/{id}/dates</c>. Each property
/// is optional; the handler applies only the supplied ones in the order
/// Delivery → InitialEvaluation → ProductionDeploy. See
/// <c>contracts/openapi.yaml#MilestoneDatesRequest</c> for the wire shape.
/// </summary>
/// <param name="OrderId">Aggregate identifier of the target order.</param>
/// <param name="DeliveryDate">Drives Approved → InProgress when supplied.</param>
/// <param name="InitialEvaluationDate">Recorded without changing the workflow status.</param>
/// <param name="ProductionDeployDate">Drives InProgress → Deployed when supplied.</param>
/// <param name="PostDeployScreenshotPath">Optional post-deploy evidence path; paired with <paramref name="ProductionDeployDate"/>.</param>
public sealed record RecordMilestoneDatesCommand(
    Guid OrderId,
    DateTime? DeliveryDate,
    DateTime? InitialEvaluationDate,
    DateTime? ProductionDeployDate,
    string? PostDeployScreenshotPath);
