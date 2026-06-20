namespace ChangeOrder.Business.Commands.SetOrderDates;

/// <summary>
/// Command para actualizar las fechas de seguimiento de una orden de cambio
/// </summary>

public sealed record SetOrderDatesCommand(

    Guid Id,
    DateTime? DeliveryDate,
    DateTime? InitialEvaluationDate,
    DateTime? ProductionDeployDate

    );
