namespace ChangeOrder.Domain.Entities;

/// <summary>
/// Editable content carrier used by <c>ChangeOrder</c> creation and update
/// flows. Kept as a record so the &lt;=3 parameter rule does not apply (records
/// are data carriers per CLAUDE.md code preferences).
/// </summary>
/// <param name="ProgramName">Subject application name.</param>
/// <param name="ProductionVersion">Pre-change production version.</param>
/// <param name="VersionScreenshotPath">Optional pre-change evidence path.</param>
/// <param name="WorkDescription">Short description.</param>
/// <param name="RequestDetails">Detailed specification.</param>
/// <param name="Justification">Business justification.</param>
/// <param name="RequiredAction">Required action description.</param>
public sealed record ChangeOrderContent(
    string ProgramName,
    string ProductionVersion,
    string? VersionScreenshotPath,
    string WorkDescription,
    string RequestDetails,
    string Justification,
    string RequiredAction);
