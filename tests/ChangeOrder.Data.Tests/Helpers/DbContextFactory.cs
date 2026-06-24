using ChangeOrder.Data.Context;
using ChangeOrder.Data.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace ChangeOrder.Data.Tests.Helpers;

/// <summary>
/// Crea instancias aisladas de <see cref="ChangeOrderDbContext"/> para tests de integración.
/// Cada llamada genera una base de datos InMemory con nombre único para evitar contaminación entre tests.
/// </summary>
internal static class DbContextFactory
{
    /// <summary>
    /// Crea un contexto limpio sin interceptores — suficiente para tests de lectura/escritura directa.
    /// </summary>
    internal static ChangeOrderDbContext Create()
    {
        DbContextOptions<ChangeOrderDbContext> options = new DbContextOptionsBuilder<ChangeOrderDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ChangeOrderDbContext(options);
    }

    /// <summary>
    /// Crea un contexto con <see cref="AuditInterceptor"/> registrado —
    /// necesario para tests que verifican soft-delete y timestamps de auditoría.
    /// </summary>
    internal static ChangeOrderDbContext CreateWithAudit()
    {
        AuditInterceptor interceptor = new();

        DbContextOptions<ChangeOrderDbContext> options = new DbContextOptionsBuilder<ChangeOrderDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        return new ChangeOrderDbContext(options);
    }
}
