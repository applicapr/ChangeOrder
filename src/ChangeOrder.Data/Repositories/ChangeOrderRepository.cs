using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Data.Context;
using Microsoft.EntityFrameworkCore;

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
    /// Marca la orden para borrado — el AuditInterceptor lo convierte en soft-delete.
    /// </summary>

    public void Delete(ChangeOrderEntity order)
    {
        _context.ChangeOrders.Remove(order);
    }
}
