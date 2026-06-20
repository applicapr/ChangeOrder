namespace ChangeOrder.Business.Commands.SetApproval;

/// <summary>
/// Command para registrar el veredicto de un nivel de aprobación
/// </summary>
public sealed record SetApprovalCommand(

    Guid Id,
    string Level,
    string Verdict

);
