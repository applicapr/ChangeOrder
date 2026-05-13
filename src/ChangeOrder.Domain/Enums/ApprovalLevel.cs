namespace ChangeOrder.Domain.Enums;

/// <summary>Identifier of one of the four approval slots on the approval chain.</summary>
public enum ApprovalLevel
{
    /// <summary>The requester's own confirmation slot.</summary>
    Requester,

    /// <summary>Department head slot.</summary>
    DepartmentHead,

    /// <summary>IT head slot.</summary>
    ItHead,

    /// <summary>Programming division slot.</summary>
    ProgrammingDivision
}
