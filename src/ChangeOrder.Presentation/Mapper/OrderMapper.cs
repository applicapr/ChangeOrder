using ChangeOrder.Domain.Entities;
using ChangeOrder.Presentation.DTOs.Responses;

namespace ChangeOrder.Presentation.Mappers;

/// <summary>
/// Mapper manual para convertir entidades en DTOs de respuesta.
/// </summary>
public static class OrderMapper
{
    /// <summary>
    /// Convierte una entidad en un OrderResponse.
    /// </summary>
    public static OrderResponse ToResponse(ChangeOrderEntity entity) => new(
        Id: entity.Id,
        OrderNumber: entity.Number.Value,
        ProgramName: entity.ProgramName,
        RequestDate: entity.RequestDate,
        RequesterName: entity.Requester.Name,
        Status: entity.Status.ToString(),
        RowVersion: entity.RowVersion);

    /// <summary>
    /// Convierte una lista de entidades en una lista de OrderResponse.
    /// </summary>
    public static IReadOnlyList<OrderResponse> ToResponseList(
        IEnumerable<ChangeOrderEntity> entities) =>
        entities.Select(ToResponse).ToList().AsReadOnly();
}
