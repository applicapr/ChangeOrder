using ChangeOrder.Data.Context;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ChangeOrder.Data.Repositories;

/// <summary>
/// Implementación del repositorio de órdenes de cambio usando EF Core.
/// </summary>

public sealed class ChangeOrderRepository : IChangeOrderRepository
{
    private readonly ChangeOrderDbContext _context;

    /// <summary>
    /// Inicializa el repositorio con el DbContext.
    /// </summary>

    public ChangeOrderRepository(ChangeOrderDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Obtiene una orden por Id. Retorna null si no existe o está soft-deleted.
    /// </summary>

    public async Task<ChangeOrderEntity?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _context.ChangeOrders
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    /// <summary>
    /// Obtiene todas las órdenes de cambio.
    /// </summary>

    public async Task<IReadOnlyList<ChangeOrderEntity>> GetAllAsync(CancellationToken ct)
    {
        return await _context.ChangeOrders.ToListAsync(ct);
    }

    /// <summary>
    /// Obtiene todas las órdenes de una fecha específica.
    /// </summary>

    public async Task<IReadOnlyList<ChangeOrderEntity>> GetByDateAsync(DateTime date, CancellationToken ct)
    {
        return await _context.ChangeOrders
            .Where(x => x.RequestDate.Date == date.Date)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Obtiene el próximo número secuencial del día para generar el OrderNumber. 
    /// </summary>

    public async Task<int> GetNextSequenceForDateAsync(DateTime date, CancellationToken ct)
    {
        int count = await _context.ChangeOrders
            .CountAsync(x => x.RequestDate.Date == date.Date, ct);
        return count + 1;

    }

    /// <summary>
    /// Agrega una nueva orden al contexto sin guardar — lo maneja el UnitOfWork.
    /// </summary>

    public async Task AddAsync(ChangeOrderEntity order, CancellationToken ct)
    {
        await _context.ChangeOrders.AddAsync(order, ct);
    }

    /// <summary>
    /// Marca la orden como modificada en el ChangeTracker de EF Core.
    /// </summary>

    public void Update(ChangeOrderEntity order)
    {
        _context.ChangeOrders.Update(order);
    }

    /// <summary>
    /// Sobrescribe el OriginalValue del rowVersion con el valor que el cliente afirma tener.
    /// EF Core genera: WHERE Id = @id AND RowVersion = @clientRowVersion
    /// Si la fila cambió en la BD desde que el cliente la leyó, se lanzan 0 filas afectadas
    /// → DbUpdateConcurrencyException → ConcurrencyException del dominio.
    /// </summary>

    public void UpdateWithConcurrencyToken(ChangeOrderEntity order, byte[] clientRowVersion)
    {
        EntityEntry<ChangeOrderEntity> entry = _context.Entry(order);
        entry.Property(x => x.RowVersion).OriginalValue = clientRowVersion;

        if (entry.State == EntityState.Unchanged)
            entry.State = EntityState.Modified;
    }

    /// <summary>
    /// Marca la orden para borrado — el AuditInterceptor lo convierte en soft-delete.
    /// </summary>

    public void Delete(ChangeOrderEntity order)
    {
        _context.ChangeOrders.Remove(order);
    }

    /// <summary>
    /// Busca un registro de idempotencia por su Key
    /// </summary>

    public async Task<IdempotencyRecord?> GetIdempotencyRecordAsync(Guid key, CancellationToken ct)
    {
        return await _context.IdempotencyRecords
            .FirstOrDefaultAsync(x => x.Key == key, ct);
    }

    /// <summary>
    /// Agrega un nuevo registro de idempotencia sin guardar — lo maneja el UnitOfWork.
    /// </summary>

    public async Task AddIdempotencyRecordAsync(IdempotencyRecord record, CancellationToken ct)
    {
        await _context.IdempotencyRecords.AddAsync(record, ct);
    }

    /// <summary>
    /// Ejecuta un DELETE directo en SQL para los registros de idempotencia anteriores
    /// a <paramref name="olderThan"/>. No usa el ChangeTracker ni requiere SaveChangesAsync.
    /// Retorna la cantidad de filas eliminadas.
    /// </summary>

    public async Task<int> DeleteOldIdempotencyRecordsAsync(DateTime olderThan, CancellationToken ct)
    {
        return await _context.IdempotencyRecords
            .Where(x => x.CreatedAt < olderThan)
            .ExecuteDeleteAsync(ct);
    }
}
