using System.Net;
using ChangeOrder.Data.Persistence;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Host;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Xunit;

namespace ChangeOrder.Presentation.Tests.Endpoints;

/// <summary>
/// Exercises SC-007: <c>GET /health</c> returns 200 while SQL Server is
/// reachable and 503 otherwise. The dependency is exercised through a test
/// double — the real <c>AddSqlServer</c> probe is replaced with a controllable
/// <see cref="IHealthCheck"/> so the test does not need a live SQL instance.
/// </summary>
public sealed class HealthCheckTests
{
    [Fact]
    public async Task Health_WhenSqlIsHealthy_Returns200()
    {
        using HealthCheckWebApplicationFactory factory = new(HealthStatus.Healthy);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_WhenSqlIsUnhealthy_Returns503()
    {
        using HealthCheckWebApplicationFactory factory = new(HealthStatus.Unhealthy);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    /// <summary>
    /// Stubbed <see cref="IHealthCheck"/> that returns a deterministic status
    /// captured at construction time. Lets the test toggle the health verdict
    /// without spinning up SQL Server.
    /// </summary>
    private sealed class StubHealthCheck : IHealthCheck
    {
        private readonly HealthStatus _status;

        public StubHealthCheck(HealthStatus status)
        {
            _status = status;
        }

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            HealthCheckResult result = _status switch
            {
                HealthStatus.Healthy => HealthCheckResult.Healthy("SQL Server stub: healthy"),
                HealthStatus.Degraded => HealthCheckResult.Degraded("SQL Server stub: degraded"),
                _ => HealthCheckResult.Unhealthy("SQL Server stub: unhealthy")
            };
            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// Test-host factory that swaps the production SQL Server health check for
    /// a <see cref="StubHealthCheck"/> driven by the constructor verdict.
    /// </summary>
    private sealed class HealthCheckWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly HealthStatus _verdict;

        public HealthCheckWebApplicationFactory(HealthStatus verdict)
        {
            _verdict = verdict;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                Dictionary<string, string?> overrides = new()
                {
                    ["ConnectionStrings:DefaultConnection"] = "Server=test;Database=test;Trusted_Connection=false;Encrypt=false"
                };
                config.AddInMemoryCollection(overrides);
            });
            builder.ConfigureServices(services =>
            {
                List<ServiceDescriptor> efDescriptors = services
                    .Where(d =>
                        d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)
                        || d.ServiceType == typeof(DbContextOptions)
                        || d.ServiceType == typeof(ApplicationDbContext)
                        || (d.ServiceType.FullName?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ?? false))
                    .ToList();
                foreach (ServiceDescriptor descriptor in efDescriptors)
                {
                    services.Remove(descriptor);
                }

                services.RemoveAll<IUnitOfWork>();
                services.RemoveAll<IChangeOrderRepository>();
                services.AddDbContext<ApplicationDbContext>(opts =>
                    opts.UseInMemoryDatabase($"changeorder-healthcheck-{Guid.NewGuid():N}"));
                services.AddScoped<IUnitOfWork, InMemoryUnitOfWork>();
                services.AddScoped<IChangeOrderRepository, InMemoryChangeOrderRepository>();

                // The production registration goes through
                // `IConfigureOptions<HealthCheckServiceOptions>` (added by
                // `AddSqlServer`). Drop those configure-options descriptors,
                // plus any direct HealthCheckRegistration descriptors, and
                // re-add a single stub.
                List<ServiceDescriptor> existingHealthCheckBindings = services
                    .Where(d =>
                        d.ServiceType == typeof(IConfigureOptions<HealthCheckServiceOptions>)
                        || d.ServiceType == typeof(HealthCheckRegistration))
                    .ToList();
                foreach (ServiceDescriptor descriptor in existingHealthCheckBindings)
                {
                    services.Remove(descriptor);
                }

                HealthStatus verdict = _verdict;
                services
                    .AddHealthChecks()
                    .AddCheck("sqlserver-stub", new StubHealthCheck(verdict));
            });
        }
    }
}
