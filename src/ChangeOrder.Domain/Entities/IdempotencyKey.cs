namespace ChangeOrder.Domain.Entities;

/// <summary>
/// Persistence row used to deduplicate retried <c>POST /change-orders</c>
/// submissions within the 24-hour retention window (research.md R-2).
/// Intentionally NOT audited and NOT soft-deletable; the cleanup background
/// service hard-deletes expired rows.
/// </summary>
public sealed class IdempotencyKey
{
    /// <summary>Client-supplied idempotency key (8-64 chars).</summary>
    public string Key { get; private set; } = default!;

    /// <summary>Foreign key into <c>ChangeOrders.Id</c> for the order produced by the original successful POST.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>SHA-256 of the canonicalized request body (used to detect payload divergence on replay).</summary>
    public byte[] RequestHash { get; private set; } = default!;

    /// <summary>UTC instant at which the key was first stored.</summary>
    public DateTime CreatedAt { get; private set; }

    private IdempotencyKey() { }

    /// <summary>Builds a fresh idempotency record.</summary>
    public IdempotencyKey(string key, Guid orderId, byte[] requestHash, DateTime createdAtUtc)
    {
        Key = key;
        OrderId = orderId;
        RequestHash = requestHash;
        CreatedAt = createdAtUtc;
    }
}
