using ChangeOrder.Domain.Entities;

namespace ChangeOrder.Domain.Abstractions;

/// <summary>
/// Contrato de acceso a datos para las órdenes de cambio
/// </summary>

public interface IChangeOrderRepository
{

    /// <summary>
    /// Obtiene una orden por Id. Retorna null si no existe o está soft-deleted
    /// </summary>
    public Task<ChangeOrderEntity?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Obtiene todas las órdenes de cambio.
    /// </summary>
    public Task<IReadOnlyList<ChangeOrderEntity>> GetAllAsync(CancellationToken ct);

    /// <summary>
    /// Obtiene las órdenes de una fecha específica
    /// </summary>
    public Task<IReadOnlyList<ChangeOrderEntity>> GetByDateAsync(DateTime date, CancellationToken ct);

    /// <summary>
    /// Obtiene el próximo número secuencial del día para generar el OrderNumber.
    /// </summary>
    public Task<int> GetNextSequenceForDateAsync(DateTime date, CancellationToken ct);

    /// <summary>
    /// Agrega una nueva orden al contexto sin guardar — lo maneja el UnitOfWork.
    /// </summary>
    public Task AddAsync(ChangeOrderEntity order, CancellationToken ct);

    /// <summary>
    /// Marca la entidad como modificada en el ChangeTracker de EF Core.
    /// </summary>
    public void Update(ChangeOrderEntity order);

    /// <summary>
    /// Establece el OriginalValue del token de concurrencia en el ChangeTracker
    /// con el rowVersion que el cliente afirma tener. EF Core usará ese valor
    /// en la cláusula WHERE del UPDATE, de modo que si la fila ya cambió en la BD
    /// se lanzará DbUpdateConcurrencyException → ConcurrencyException.
    /// </summary>
    public void UpdateWithConcurrencyToken(ChangeOrderEntity order, byte[] clientRowVersion);

    /// <summary>
    /// Marca la orden para borrado — el AuditInterceptor lo convierte en soft-delete.
    /// </summary>
    public void Delete(ChangeOrderEntity order);

}
