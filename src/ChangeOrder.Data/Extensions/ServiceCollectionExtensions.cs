using ChangeOrder.Data.Context;
using ChangeOrder.Data.Interceptors;
using ChangeOrder.Data.Repositories;
using ChangeOrder.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeOrder.Data.Extensions;

/// <summary>
/// Extensiones de IServiceCollection para registrar los servicios de la capa Data.
/// </summary>

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra el DbContext, AuditInterceptor y repositorios en el contenedor de DI.
    /// </summary>

    public static IServiceCollection AddDataServices(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddSingleton<AuditInterceptor>();
        services.AddDbContext<ChangeOrderDbContext>((sp, options) =>
            options.UseSqlServer(connectionString)
                   .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));
        services.AddScoped<IChangeOrderRepository, ChangeOrderRepository>();
        services.AddScoped<IUnitOfWork, ChangeOrderDbContext>();
        return services;
    }
}
