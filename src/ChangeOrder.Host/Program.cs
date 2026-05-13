using System.Globalization;
using ChangeOrder.Business.Extensions;
using ChangeOrder.Data.Extensions;
using ChangeOrder.Domain.Extensions;
using ChangeOrder.Presentation.Extensions;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;
using Serilog;

namespace ChangeOrder.Host;

/// <summary>
/// Application entry point. Owns the composition root: bootstraps Serilog,
/// stacks <c>AddDomain → AddDataLayer → AddBusinessLayer → AddPresentationLayer</c>,
/// wires health checks and maps the change-order Minimal-API group.
/// </summary>
/// <remarks>
/// Declared as <c>partial</c> (non-static) so
/// <c>WebApplicationFactory&lt;Program&gt;</c> from
/// <c>Microsoft.AspNetCore.Mvc.Testing</c> can pick up this type as the
/// integration-test entry point. The class has no instance state and is
/// not intended to be instantiated outside the test infrastructure.
/// </remarks>
public partial class Program
{
    /// <summary>Builds and runs the web host.</summary>
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
            .CreateBootstrapLogger();

        try
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            builder.Host.UseSerilog((context, services, configuration) =>
                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
                    .WriteTo.File(
                        path: "logs/changeorder-.log",
                        formatProvider: CultureInfo.InvariantCulture,
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 14));

            builder.Services
                .AddDomain()
                .AddDataLayer(builder.Configuration)
                .AddBusinessLayer()
                .AddPresentationLayer();

            string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                builder.Services
                    .AddHealthChecks()
                    .AddSqlServer(connectionString, name: "sqlserver");
            }
            else
            {
                builder.Services.AddHealthChecks();
            }

            WebApplication app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference(options =>
                {
                    options.WithTitle("ChangeOrder.Api");
                });
            }

            app.UseHttpsRedirection();
            app.UseSerilogRequestLogging();
            app.UseRateLimiter();
            app.MapHealthChecks("/health");
            app.MapVersionEndpoint();
            app.MapChangeOrderApi();

            app.Run();
        }
        catch (Exception ex) when (ex is not HostAbortedException)
        {
            // HostAbortedException is thrown by EF Core design-time tooling
            // (e.g. `dotnet ef database update`) which intentionally aborts the
            // host after resolving the DbContext. Letting it bypass this catch
            // keeps the migrations sidecar logs clean of spurious [FTL] entries.
            Log.Fatal(ex, "ChangeOrder.Host terminated unexpectedly.");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
