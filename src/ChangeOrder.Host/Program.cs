using System.Globalization;
using ChangeOrder.Data.Extensions;
using ChangeOrder.Domain.Extensions;
using ChangeOrder.Business.Extensions;
using ChangeOrder.Presentation.Extensions;
using Serilog;

namespace ChangeOrder.Host;

/// <summary>
/// Application entry point. Owns the composition root: bootstraps Serilog,
/// stacks <c>AddDomain → AddDataLayer → AddBusinessLayer → AddPresentationLayer</c>,
/// wires health checks and maps the change-order Minimal-API group.
/// </summary>
public static class Program
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
            }

            app.UseHttpsRedirection();
            app.UseSerilogRequestLogging();
            app.UseRateLimiter();
            app.MapHealthChecks("/health");
            app.MapChangeOrderApi();

            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "ChangeOrder.Host terminated unexpectedly.");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
