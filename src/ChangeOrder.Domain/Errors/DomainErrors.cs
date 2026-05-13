using System.Globalization;
using ChangeOrder.Domain.Enums;

namespace ChangeOrder.Domain.Errors;

/// <summary>
/// Catalog of every domain-level <see cref="Error"/> produced by the application.
/// Codes are stable contract — the Presentation layer maps them 1:1 onto RFC 7807
/// <c>ProblemDetails</c> payloads (see <c>contracts/openapi.yaml</c>).
/// </summary>
public static class DomainErrors
{
    /// <summary>Errors related to the <c>ChangeOrder</c> aggregate.</summary>
    public static class Order
    {
        /// <summary>Order id was not found (HTTP 404).</summary>
        public static Error NotFound(Guid id)
            => new(
                "order.not_found",
                string.Format(CultureInfo.InvariantCulture, "Order {0} not found.", id));

        /// <summary>The candidate <c>OrderNumber</c> already exists (HTTP 409). Surfaced after the retry budget is exhausted (R-1).</summary>
        public static Error DuplicateNumber(string number)
            => new(
                "order.duplicate_number",
                string.Format(CultureInfo.InvariantCulture, "OrderNumber {0} already exists.", number));

        /// <summary>Requested status transition is not allowed by the state machine (HTTP 409).</summary>
        public static Error InvalidStateTransition(OrderStatus from, OrderStatus to)
            => new(
                "order.invalid_transition",
                string.Format(CultureInfo.InvariantCulture, "Cannot move from {0} to {1}.", from, to));

        /// <summary>PUT attempted on an order that has left <see cref="OrderStatus.Draft"/> (HTTP 409, FR-006 / C-1).</summary>
        public static Error EditAfterDraft()
            => new(
                "order.edit_after_draft",
                "PUT is only allowed while the order is in Draft (FR-006).");

        /// <summary>Daily sequence has reached the maximum of 99 (HTTP 409).</summary>
        public static Error DailySequenceExhausted(DateOnly date)
            => new(
                "order.daily_sequence_exhausted",
                string.Format(CultureInfo.InvariantCulture, "More than 99 orders requested for {0:yyyy-MM-dd}.", date));

        /// <summary>The submitted concurrency token does not match the persisted value (HTTP 409, FR-013).</summary>
        public static Error ConcurrencyConflict()
            => new(
                "order.concurrency_conflict",
                "The order was modified by another process. Refetch the latest version and retry.");
    }

    /// <summary>Errors related to <c>Idempotency-Key</c> processing on POST.</summary>
    public static class Idempotency
    {
        /// <summary>Same <c>Idempotency-Key</c> was previously used with a different payload (HTTP 422).</summary>
        public static Error PayloadDivergence(string key)
            => new(
                "idempotency.payload_divergence",
                string.Format(CultureInfo.InvariantCulture, "Idempotency-Key {0} was previously used with a different payload.", key));
    }
}
