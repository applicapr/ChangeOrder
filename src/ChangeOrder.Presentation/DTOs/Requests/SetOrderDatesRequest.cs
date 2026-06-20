namespace ChangeOrder.Presentation.DTOs.Requests;

/// <summary>
/// Datos que el cliente envía para actualizar las fechas de seguimiento de una orden
/// </summary>
public sealed record SetOrderDatesRequest(

    DateTime? DeliveryDate,
    DateTime? InitialEvaluationDate,
    DateTime? ProductionDeployDate

);
