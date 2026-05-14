namespace ChangeOrder.Business.Commands.UpdateOrder;

/// <summary>
/// CQRS command for <c>PUT /api/v1/change-orders/{id}</c>. Carries the new
/// editable content plus the client-supplied <see cref="RowVersion"/> token
/// used to enforce optimistic concurrency (FR-013). The handler only accepts
/// the mutation while the order remains in <c>Draft</c> (FR-006 / C-1).
/// </summary>
/// <param name="OrderId">Aggregate identifier of the target order.</param>
/// <param name="RowVersion">SQL Server <c>rowversion</c> previously returned by a GET.</param>
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
public sealed record UpdateOrderCommand(
    Guid OrderId,
    byte[] RowVersion,
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
