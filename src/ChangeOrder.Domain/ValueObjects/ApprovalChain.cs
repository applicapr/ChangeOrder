using ChangeOrder.Domain.Enums;

namespace ChangeOrder.Domain.ValueObjects;

/// <summary>
/// Cadena de Aprobacion sw 4 niveles jerarquico
/// todas las aprobaciones inician en Pending
/// </summary>

public sealed record ApprovalChain(

    ApprovalStatus RequesterApproval = ApprovalStatus.Pending,
    ApprovalStatus DepartmentHeadApproval = ApprovalStatus.Pending,
    ApprovalStatus ItHeadApproval = ApprovalStatus.Pending,
    ApprovalStatus ProgrammingDivisionApproval = ApprovalStatus.Pending

    );
