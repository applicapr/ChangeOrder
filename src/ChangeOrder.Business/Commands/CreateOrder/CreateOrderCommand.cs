namespace ChangeOrder.Business.Commands.CreateOrder;

/// <summary>
/// CQRS command that carries every field needed to create a new
/// <c>ChangeOrder</c> aggregate, plus the client-supplied
/// <c>Idempotency-Key</c> header (R-2).
/// </summary>
/// <param name="IdempotencyKey">Client-generated key used to deduplicate retried submissions.</param>
/// <param name="ProgramName">Subject application name.</param>
/// <param name="ProductionVersion">Pre-change production version.</param>
/// <param name="VersionScreenshotPath">Optional pre-change evidence path.</param>
/// <param name="WorkDescription">Short description of the work to be performed.</param>
/// <param name="RequestDetails">Detailed specification of the change.</param>
/// <param name="Justification">Business justification.</param>
/// <param name="RequiredAction">Required action description.</param>
/// <param name="RequesterName">Requester full name.</param>
/// <param name="RequesterPosition">Requester job title.</param>
/// <param name="RequesterDepartment">Requester organizational department.</param>
/// <param name="RequesterEmail">Requester contact e-mail.</param>
public sealed record CreateOrderCommand(
    string IdempotencyKey,
    string ProgramName,
    string ProductionVersion,
    string? VersionScreenshotPath,
    string WorkDescription,
    string RequestDetails,
    string Justification,
    string RequiredAction,
    string RequesterName,
    string RequesterPosition,
    string RequesterDepartment,
    string RequesterEmail);
