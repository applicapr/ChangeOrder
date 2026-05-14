namespace ChangeOrder.Business.Commands.DeleteOrder;

/// <summary>
/// CQRS command for <c>DELETE /api/v1/change-orders/{id}</c>. Triggers a
/// soft delete: the handler invokes <c>IChangeOrderRepository.Remove</c>; the
/// Data-layer <c>AuditInterceptor</c> intercepts <see cref="object"/> entries
/// in <c>EntityState.Deleted</c> and flips them to <c>Modified</c> with
/// <c>IsDeleted = true</c> + <c>DeletedAt = nowUtc</c> (FR-008/009).
/// </summary>
/// <param name="OrderId">Aggregate identifier of the target order.</param>
public sealed record DeleteOrderCommand(Guid OrderId);
