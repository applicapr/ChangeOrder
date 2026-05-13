using ChangeOrder.Domain.Enums;

namespace ChangeOrder.Domain.ValueObjects;

/// <summary>
/// Four-slot approval chain embedded on <c>ChangeOrder</c>. Each slot holds a
/// single <see cref="ApprovalStatus"/>. Persisted as four flattened
/// <c>nvarchar(20)</c> columns via EF Core <c>OwnsOne</c>.
/// </summary>
/// <param name="RequesterApproval">Verdict from the requester.</param>
/// <param name="DepartmentHeadApproval">Verdict from the department head.</param>
/// <param name="ItHeadApproval">Verdict from the IT head.</param>
/// <param name="ProgrammingDivisionApproval">Verdict from the programming division.</param>
public sealed record ApprovalChain(
    ApprovalStatus RequesterApproval,
    ApprovalStatus DepartmentHeadApproval,
    ApprovalStatus ItHeadApproval,
    ApprovalStatus ProgrammingDivisionApproval)
{
    /// <summary>The default chain that every newly created order carries: all <see cref="ApprovalStatus.Pending"/>.</summary>
    public static ApprovalChain Empty { get; } = new(
        ApprovalStatus.Pending,
        ApprovalStatus.Pending,
        ApprovalStatus.Pending,
        ApprovalStatus.Pending);

    /// <summary>Returns <c>true</c> when every slot is <see cref="ApprovalStatus.Approved"/>.</summary>
    public bool AllApproved() =>
        RequesterApproval == ApprovalStatus.Approved
        && DepartmentHeadApproval == ApprovalStatus.Approved
        && ItHeadApproval == ApprovalStatus.Approved
        && ProgrammingDivisionApproval == ApprovalStatus.Approved;

    /// <summary>Returns <c>true</c> when any slot has been rejected.</summary>
    public bool AnyRejected() =>
        RequesterApproval == ApprovalStatus.Rejected
        || DepartmentHeadApproval == ApprovalStatus.Rejected
        || ItHeadApproval == ApprovalStatus.Rejected
        || ProgrammingDivisionApproval == ApprovalStatus.Rejected;
}
