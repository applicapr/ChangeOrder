namespace ChangeOrder.Business.Services;

/// <summary>
/// Discriminated outcome returned by <see cref="IdempotencyService"/>. The
/// command handler branches on the concrete subtype to decide whether to
/// short-circuit (replay), reject (payload divergence), or proceed with a
/// fresh creation.
/// </summary>
public abstract record IdempotencyOutcome
{
    private IdempotencyOutcome() { }

    /// <summary>The key was previously stored and the body hash matches; replay the persisted order.</summary>
    /// <param name="OrderId">Id of the order produced by the original successful POST.</param>
    public sealed record Existing(Guid OrderId) : IdempotencyOutcome;

    /// <summary>The key was previously stored but the body hash differs (HTTP 422 — R-2).</summary>
    public sealed record Conflict() : IdempotencyOutcome;

    /// <summary>The key has not been used before; the caller must persist a new record with the supplied hash.</summary>
    /// <param name="Hash">SHA-256 of the canonicalized request body (32 bytes).</param>
    public sealed record Fresh(byte[] Hash) : IdempotencyOutcome;
}
