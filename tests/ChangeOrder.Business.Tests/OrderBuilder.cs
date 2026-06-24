using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Enums;
using ChangeOrder.Domain.ValueObjects;

namespace ChangeOrder.Business.Tests;

/// <summary>
/// Construye instancias de <see cref="ChangeOrderEntity"/> válidas para los tests.
/// </summary>
internal static class OrderBuilder
{
    /// <summary>
    /// Retorna una <see cref="ChangeOrderEntity"/> con todos los campos obligatorios satisfechos.
    /// </summary>
    internal static ChangeOrderEntity Build(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Number = OrderNumber.Create(DateTime.UtcNow, 1),
        ProgramName = "TestApp",
        ProductionVersion = "v1.0.0",
        RequestDate = DateTime.UtcNow,
        WorkDescription = "Test work description",
        RequestDetails = "Test request details",
        Justification = "Test justification",
        RequiredAction = "Test required action",
        Requester = new RequesterInfo("Test User", "Developer", "IT", "test@example.com"),
        Approval = new ApprovalChain(),
        Status = OrderStatus.Draft,
        CreatedAt = DateTime.UtcNow,
        RowVersion = Array.Empty<byte>()
    };
}
