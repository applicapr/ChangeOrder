namespace ChangeOrder.Domain.Entities;

/// <summary>
/// Registro de idempotencia para prevenir creación de órdenes duplicadas
/// por reintentos del cliente. Se retiene por 24 horas.
/// </summary>
public sealed class IdempotencyRecord
{
    /// <summary>
    /// La Idempotency-Key enviada por el cliente en el header.
    /// </summary>
    public required Guid Key { get; init; }

    /// <summary>
    /// Hash SHA-256 del payload canonicalizado — detecta si el cliente
    /// reusa la misma Key con datos diferentes.
    /// </summary>
    public required string PayloadHash { get; init; }

    /// <summary>
    /// El Id de la orden creada, para poder responder en un replay.
    /// </summary>
    public required Guid ResourceId { get; init; }

    /// <summary>
    /// Timestamp de creación en UTC — usado para limpieza después de 24h.
    /// </summary>
    public required DateTime CreatedAt { get; init; }
}
