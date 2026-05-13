using ChangeOrder.Domain.Enums;

namespace ChangeOrder.Business.Commands.RecordApproval;

/// <summary>
/// CQRS command that records a single verdict on one of the four slots of the
/// approval chain (see <c>data-model.md §8</c> and OpenAPI
/// <c>ApprovalVerdictRequest</c>). The handler enforces the state-machine
/// rules — the command only carries data.
/// </summary>
/// <param name="OrderId">Aggregate identifier of the target order.</param>
/// <param name="Level">Approval slot to update (<see cref="ApprovalLevel"/>).</param>
/// <param name="Verdict">Verdict applied to the slot (<see cref="ApprovalStatus"/>).</param>
public sealed record RecordApprovalCommand(
    Guid OrderId,
    ApprovalLevel Level,
    ApprovalStatus Verdict);
