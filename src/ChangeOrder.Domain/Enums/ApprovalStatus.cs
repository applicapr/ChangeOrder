namespace ChangeOrder.Domain.Enums;

/// <summary>
/// Verdict recorded for each of the four slots of the approval chain.
/// </summary>
public enum ApprovalStatus
{
    /// <summary>Default state; no verdict recorded yet.</summary>
    Pending,

    /// <summary>Slot approved.</summary>
    Approved,

    /// <summary>Slot rejected.</summary>
    Rejected
}
