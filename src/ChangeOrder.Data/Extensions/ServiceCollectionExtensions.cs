using ChangeOrder.Data.Interceptors;
using ChangeOrder.Data.Persistence;
using ChangeOrder.Data.Repositories;
using ChangeOrder.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeOrder.Data.Extensions;

/// <summary>Composition entry-point for the Data layer.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the EF Core DbContext (SQL Server), the <c>AuditInterceptor</c>,
    /// the repository implementation and the unit-of-work wrapper.
    /// </summary>
    public static IServiceCollection AddDataLayer(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<AuditInterceptor>();

        services.AddDbContext<ApplicationDbContext>((serviceProvider, optionsBuilder) =>
        {
            string connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' is not configured.");
            optionsBuilder
                .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
                .AddInterceptors(serviceProvider.GetRequiredService<AuditInterceptor>());
        });

        services.AddScoped<IChangeOrderRepository, ChangeOrderRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IdempotencyKeyCleanupRepository>();

        return services;
    }
}
