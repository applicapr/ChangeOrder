using DomainChangeOrder = ChangeOrder.Domain.Entities.ChangeOrder;

namespace ChangeOrder.Business.Commands.CreateOrder;

/// <summary>
/// Success payload produced by <see cref="CreateOrderHandler"/>. Carries the
/// resolved aggregate plus a flag the Presentation layer uses to decide
/// between <c>201 Created</c> (fresh) and <c>200 OK</c> (idempotent replay).
/// </summary>
/// <param name="Order">The persisted aggregate to be returned to the caller.</param>
/// <param name="WasReplay"><c>true</c> when the handler short-circuited because the <c>Idempotency-Key</c> already existed.</param>
public sealed record CreateOrderResult(DomainChangeOrder Order, bool WasReplay);
