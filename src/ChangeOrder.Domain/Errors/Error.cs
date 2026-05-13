using System.Diagnostics.CodeAnalysis;

namespace ChangeOrder.Domain.Errors;

/// <summary>
/// Represents a domain-level error with a stable machine-readable <see cref="Code"/>
/// and a human-readable <see cref="Message"/>.
/// </summary>
/// <remarks>
/// Errors are produced by the <see cref="DomainErrors"/> catalog and surfaced to the
/// caller via <see cref="Result{TValue, TError}"/>. The HTTP layer translates the
/// <see cref="Code"/> into an RFC 7807 <c>ProblemDetails</c> payload (see
/// <c>ProblemDetailsFactory</c> in the Presentation layer).
/// </remarks>
/// <param name="Code">Stable identifier (e.g., <c>order.not_found</c>).</param>
/// <param name="Message">Human-readable description (English).</param>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
    Justification = "The Result-Pattern catalog uses Error by contract (research.md R-5).")]
public sealed record Error(string Code, string Message);
