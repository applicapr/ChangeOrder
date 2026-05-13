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
using Xunit;

namespace ChangeOrder.Presentation.Tests.Endpoints;

/// <summary>
/// Exercises SC-005 / research.md R-7: the fixed-window rate-limit policy
/// permits 100 requests per minute per IP and returns HTTP 429 with a
/// <c>Retry-After</c> header on the 101st hit. The policy is registered on the
/// <c>/api/v1/change-orders</c> group; all probes here hit
/// <c>GET /api/v1/change-orders</c> so they fall under the same partition.
/// </summary>
[Trait("Category", "RateLimit")]
public sealed class RateLimitTests : IClassFixture<RateLimitTests.RateLimitWebApplicationFactory>
{
    private readonly RateLimitWebApplicationFactory _factory;

    public RateLimitTests(RateLimitWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ExceedingPermitLimit_Returns429WithRetryAfterHeader()
    {
        using HttpClient client = _factory.CreateClient();

        // The first 100 GETs should pass the limiter. Whether the underlying
        // handler returns 200 or some validation error is irrelevant for this
        // test — we only care that 429 is NOT emitted until the budget is gone.
        for (int index = 0; index < 100; index++)
        {
            using HttpResponseMessage response = await client
                .GetAsync(new Uri("/api/v1/change-orders?page=1&pageSize=10", UriKind.Relative));
            response.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests,
                $"request #{index + 1} should still be within the per-minute budget");
        }

        // The 101st hit must trip the limiter.
        using HttpResponseMessage rejected = await client
            .GetAsync(new Uri("/api/v1/change-orders?page=1&pageSize=10", UriKind.Relative));

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        rejected.Headers.Contains("Retry-After").Should().BeTrue(
            "the rate-limit policy must emit Retry-After per research.md R-7");
        string? retryAfter = rejected.Headers.GetValues("Retry-After").FirstOrDefault();
        retryAfter.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Spins up a fresh host per test class so the 100-request quota starts
    /// at zero. The rate-limit policy partitions by remote IP and the in-memory
    /// test client always presents the same loopback IP, so the budget is
    /// exhausted by repeated calls from this single fixture.
    /// </summary>
    public sealed class RateLimitWebApplicationFactory : WebApplicationFactory<Program>
    {
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
                List<ServiceDescriptor> toRemove = services
                    .Where(d =>
                        d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)
                        || d.ServiceType == typeof(DbContextOptions)
                        || d.ServiceType == typeof(ApplicationDbContext)
                        || (d.ServiceType.FullName?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ?? false))
                    .ToList();
                foreach (ServiceDescriptor descriptor in toRemove)
                {
                    services.Remove(descriptor);
                }

                services.RemoveAll<IUnitOfWork>();
                services.RemoveAll<IChangeOrderRepository>();

                string databaseName = $"changeorder-ratelimit-{Guid.NewGuid():N}";
                services.AddDbContext<ApplicationDbContext>(opts =>
                    opts.UseInMemoryDatabase(databaseName));

                services.AddScoped<IUnitOfWork, InMemoryUnitOfWork>();
                services.AddScoped<IChangeOrderRepository, InMemoryChangeOrderRepository>();
            });
        }
    }
}
