namespace ChangeOrder.Domain.Enums;

/// <summary>
/// Lifecycle states of a <c>ChangeOrder</c>. Transitions are enforced by the
/// aggregate root; see <c>data-model.md §8</c> for the allowed transitions.
/// </summary>
public enum OrderStatus
{
    /// <summary>Editable; entry state.</summary>
    Draft,

    /// <summary>Submitted; waiting on the four-level approval chain.</summary>
    PendingApproval,

    /// <summary>All four approvals are <c>Approved</c>; work may begin.</summary>
    Approved,

    /// <summary>A delivery date is recorded; the change is being executed.</summary>
    InProgress,

    /// <summary>The change is live in production; terminal-success state.</summary>
    Deployed,

    /// <summary>Cancelled at any point before <see cref="Deployed"/>; terminal.</summary>
    Cancelled
}
