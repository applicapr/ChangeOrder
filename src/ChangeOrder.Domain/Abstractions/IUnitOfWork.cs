namespace ChangeOrder.Domain.Abstractions;

/// <summary>
/// Contrato del patrón Unit of Work — gestiona transacciones atómicas
/// </summary>

public interface IUnitOfWork
{
    /// <summary>
    /// Guarda todos los cambios pendientes en la base de datos
    /// </summary> 
    public Task<int> SaveChangesAsync(CancellationToken ct);
}
