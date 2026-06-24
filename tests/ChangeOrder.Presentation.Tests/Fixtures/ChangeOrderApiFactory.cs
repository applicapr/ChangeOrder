using ChangeOrder.Data.Context;
using ChangeOrder.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ChangeOrder.Presentation.Tests.Fixtures;

/// <summary>
/// Factory de integración que levanta la API con base de datos InMemory aislada.
/// </summary>
public sealed class ChangeOrderApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    /// <summary>
    /// Configura el host de test reemplazando dependencias externas por servicios en memoria.
    /// </summary>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            Dictionary<string, string?> values = new()
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=(localdb)\\mssqllocaldb;Database=ChangeOrderPresentationTests;Trusted_Connection=True;"
            };

            configuration.AddInMemoryCollection(values);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ChangeOrderDbContext>();
            services.RemoveAll<DbContextOptions<ChangeOrderDbContext>>();

            ServiceDescriptor[] dbContextConfigurationDescriptors = services
                .Where(descriptor =>
                    descriptor.ServiceType.FullName?.Contains(
                        "IDbContextOptionsConfiguration",
                        StringComparison.Ordinal) == true)
                .ToArray();

            foreach (ServiceDescriptor descriptor in dbContextConfigurationDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ChangeOrderDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            services.PostConfigure<HealthCheckServiceOptions>(options =>
                options.Registrations.Clear());

            using ServiceProvider provider = services.BuildServiceProvider();
            using IServiceScope scope = provider.CreateScope();

            ChangeOrderDbContext context = scope.ServiceProvider
                .GetRequiredService<ChangeOrderDbContext>();

            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();
        });
    }

    /// <summary>
    /// Limpia la base de datos de test.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        using IServiceScope scope = Services.CreateScope();
        ChangeOrderDbContext context = scope.ServiceProvider
            .GetRequiredService<ChangeOrderDbContext>();

        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// Persiste una orden en la base de datos de test.
    /// </summary>
    public async Task SeedOrderAsync(ChangeOrderEntity order)
    {
        using IServiceScope scope = Services.CreateScope();
        ChangeOrderDbContext context = scope.ServiceProvider
            .GetRequiredService<ChangeOrderDbContext>();

        await context.ChangeOrders.AddAsync(order);
        await context.SaveChangesAsync();
    }
}
