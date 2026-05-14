namespace ChangeOrder.Presentation.DTOs.Responses;

/// <summary>
/// Wire shape returned by every <c>ChangeOrder</c>-producing endpoint.
/// Matches the <c>OrderResponse</c> schema in <c>contracts/openapi.yaml</c>.
/// </summary>
/// <param name="Id">Aggregate identifier.</param>
/// <param name="OrderNumber">Public <c>yyyyMMdd-##</c> identifier.</param>
/// <param name="Status">Current workflow state.</param>
/// <param name="ProgramName">Subject application name.</param>
/// <param name="ProductionVersion">Pre-change production version.</param>
/// <param name="VersionScreenshotPath">Optional pre-change evidence path.</param>
/// <param name="WorkDescription">Short description.</param>
/// <param name="RequestDetails">Detailed specification.</param>
/// <param name="Justification">Business justification.</param>
/// <param name="RequiredAction">Required action description.</param>
/// <param name="Requester">Requester snapshot.</param>
/// <param name="ApprovalChain">Approval chain snapshot.</param>
/// <param name="RequestDate">UTC instant at which the request was submitted.</param>
/// <param name="DeliveryDate">UTC instant of staging delivery (nullable).</param>
/// <param name="InitialEvaluationDate">UTC instant of initial QA evaluation (nullable).</param>
/// <param name="ProductionDeployDate">UTC instant of production deployment (nullable).</param>
/// <param name="PostDeployScreenshotPath">Optional post-deploy evidence path.</param>
/// <param name="CreatedAt">UTC instant at which the row was created.</param>
/// <param name="UpdatedAt">UTC instant of last update (nullable).</param>
/// <param name="RowVersion">Base64-encoded SQL Server concurrency token (FR-013).</param>
public sealed record OrderResponse(
    Guid Id,
    string OrderNumber,
    string Status,
    string ProgramName,
    string ProductionVersion,
    string? VersionScreenshotPath,
    string WorkDescription,
    string RequestDetails,
    string Justification,
    string RequiredAction,
    RequesterInfoResponse Requester,
    ApprovalChainResponse ApprovalChain,
    DateTime RequestDate,
    DateTime? DeliveryDate,
    DateTime? InitialEvaluationDate,
    DateTime? ProductionDeployDate,
    string? PostDeployScreenshotPath,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string RowVersion);
