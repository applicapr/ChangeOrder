namespace ChangeOrder.Business.Commands.DeleteOrder;

/// <summary>
/// Command para eliminar lógicamente una orden de cambio.
/// </summary>
public sealed record DeleteOrderCommand(Guid Id);
