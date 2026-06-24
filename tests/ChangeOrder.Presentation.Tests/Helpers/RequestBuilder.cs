using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Enums;
using ChangeOrder.Domain.ValueObjects;
using ChangeOrder.Presentation.DTOs.Requests;

namespace ChangeOrder.Presentation.Tests.Helpers;

/// <summary>
/// Construye requests y entidades válidas para los tests de integración HTTP.
/// </summary>
public static class RequestBuilder
{
    /// <summary>
    /// Crea un request válido para POST de órdenes de cambio.
    /// </summary>
    public static CreateOrderRequest CreateOrderRequest(Guid? idempotencyKey = null)
    {
        return new CreateOrderRequest(
            idempotencyKey ?? Guid.NewGuid(),
            "TestApp",
            "v1.0.0",
            null,
            new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc),
            "Test work",
            "Test details",
            "Test justification",
            "Test action",
            "Test User",
            "Developer",
            "IT",
            "test@example.com");
    }

    /// <summary>
    /// Crea un request inválido para validar respuesta 422.
    /// </summary>
    public static CreateOrderRequest InvalidCreateOrderRequest()
    {
        return CreateOrderRequest() with
        {
            ProgramName = string.Empty,
            RequesterEmail = "invalid-email"
        };
    }

    /// <summary>
    /// Crea un request válido para actualizar una orden.
    /// </summary>
    public static UpdateOrderRequest UpdateOrderRequest(Guid id)
    {
        return new UpdateOrderRequest(
            id,
            "UpdatedApp",
            "v2.0.0",
            null,
            "Updated work",
            "Updated details",
            "Updated justification",
            "Updated action",
            new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc),
            OrderStatus.InProgress.ToString(),
            Array.Empty<byte>());
    }

    /// <summary>
    /// Crea un request válido para actualizar fechas de seguimiento.
    /// </summary>
    public static SetOrderDatesRequest SetOrderDatesRequest()
    {
        return new SetOrderDatesRequest(
            new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 14, 0, 0, 0, DateTimeKind.Utc),
            null);
    }

    /// <summary>
    /// Crea un request válido para aprobar un nivel.
    /// </summary>
    public static SetApprovalRequest SetApprovalRequest()
    {
        return new SetApprovalRequest(ApprovalStatus.Approved.ToString());
    }

    /// <summary>
    /// Crea una entidad válida para poblar la base de datos de integración.
    /// </summary>
    public static ChangeOrderEntity Order(Guid? id = null, DateTime? requestDate = null)
    {
        DateTime date = requestDate ?? new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc);

        return new ChangeOrderEntity
        {
            Id = id ?? Guid.NewGuid(),
            Number = OrderNumber.Create(date, 1),
            ProgramName = "SeededApp",
            ProductionVersion = "v1.0.0",
            RequestDate = date,
            WorkDescription = "Seeded work",
            RequestDetails = "Seeded details",
            Justification = "Seeded justification",
            RequiredAction = "Seeded action",
            Requester = new RequesterInfo("Seed User", "Developer", "IT", "seed@example.com"),
            Approval = new ApprovalChain(),
            Status = OrderStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            RowVersion = Array.Empty<byte>()
        };
    }
}
