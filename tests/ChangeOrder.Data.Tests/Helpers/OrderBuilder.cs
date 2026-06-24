using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Enums;
using ChangeOrder.Domain.ValueObjects;

namespace ChangeOrder.Data.Tests.Helpers;

/// <summary>
/// Construye instancias de <see cref="ChangeOrderEntity"/> válidas para tests de integración.
/// </summary>
internal static class OrderBuilder
{
    /// <summary>
    /// Retorna una entidad con todos los campos obligatorios satisfechos.
    /// El parámetro <paramref name="sequence"/> diferencia el <c>OrderNumber</c> cuando
    /// se insertan múltiples entidades en la misma base de datos de test.
    /// </summary>
    internal static ChangeOrderEntity Build(int sequence = 1, DateTime? requestDate = null)
    {
        DateTime date = requestDate ?? new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        return new ChangeOrderEntity
        {
            Id = Guid.NewGuid(),
            Number = OrderNumber.Create(date, sequence),
            ProgramName = "TestApp",
            ProductionVersion = "v1.0.0",
            RequestDate = date,
            WorkDescription = "Test work",
            RequestDetails = "Test details",
            Justification = "Test justification",
            RequiredAction = "Test action",
            Requester = new RequesterInfo("Test User", "Developer", "IT", "test@example.com"),
            Approval = new ApprovalChain(),
            Status = OrderStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            RowVersion = Array.Empty<byte>()
        };
    }
}
