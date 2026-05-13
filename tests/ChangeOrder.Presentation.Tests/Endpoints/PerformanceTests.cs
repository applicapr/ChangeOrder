using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using ChangeOrder.Data.Persistence;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Host;
using ChangeOrder.Presentation.DTOs.Requests;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace ChangeOrder.Presentation.Tests.Endpoints;

/// <summary>
/// Exercises SC-002 under nominal load: 95% of <c>POST /api/v1/change-orders</c>
/// must complete in under 3 seconds end-to-end. Uses an in-process
/// <see cref="WebApplicationFactory{TEntryPoint}"/> against the EF InMemory
/// provider rather than a real SQL Server — the wall-clock budget therefore
/// measures the framework + handler overhead, NOT raw DB latency. The UPDLOCK
/// + HOLDLOCK semantics are absent here; they have their own Testcontainers
/// concurrency test.
/// </summary>
[Trait("Category", "Performance")]
public sealed class PerformanceTests : IClassFixture<PerformanceTests.PerformanceWebApplicationFactory>
{
    private readonly PerformanceWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    private const int TotalSamples = 50;
    private const int LatencyBudgetMilliseconds = 3000;
    private const double TargetPercentile = 0.95;

    public PerformanceTests(PerformanceWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task CreateOrder_Under50ParallelLoad_95PercentBelow3Seconds()
    {
        long[] elapsedMs = new long[TotalSamples];
        // Share a single HttpClient across all samples — `WebApplicationFactory`
        // freezes the Serilog bootstrap logger after the first server start, so
        // creating one client per parallel iteration races on that singleton.
        using HttpClient client = _factory.CreateClient();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, TotalSamples),
            new ParallelOptions { MaxDegreeOfParallelism = TotalSamples },
            async (index, ct) =>
            {
                string idempotencyKey = $"perf-{Guid.NewGuid():N}";

                CreateOrderRequest body = new(
                    ProgramName: "PerfApp",
                    ProductionVersion: "v1.0.0",
                    VersionScreenshotPath: null,
                    WorkDescription: "Load benchmark probe",
                    RequestDetails: "Synthetic load to measure SC-002 latency budget.",
                    Justification: "SC-002 perf budget.",
                    RequiredAction: "Apply patch.",
                    Requester: new RequesterInfoDto("Jane Doe", "QA Lead", "Quality", "jane.doe@example.com"));

                using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/change-orders")
                {
                    Content = JsonContent.Create(body)
                };
                request.Headers.Add("Idempotency-Key", idempotencyKey);

                Stopwatch stopwatch = Stopwatch.StartNew();
                using HttpResponseMessage response = await client.SendAsync(request, ct);
                stopwatch.Stop();

                response.StatusCode.Should().Be(HttpStatusCode.Created,
                    $"sample #{index} must succeed before its latency is counted");
                elapsedMs[index] = stopwatch.ElapsedMilliseconds;
            });

        Array.Sort(elapsedMs);
        int percentileIndex = (int)Math.Ceiling(TargetPercentile * TotalSamples) - 1;
        long p95 = elapsedMs[percentileIndex];

        _output.WriteLine(
            string.Create(CultureInfo.InvariantCulture,
                $"Samples={TotalSamples}, min={elapsedMs[0]} ms, max={elapsedMs[^1]} ms, p95={p95} ms"));

        p95.Should().BeLessThan(LatencyBudgetMilliseconds,
            $"SC-002 requires p95 < {LatencyBudgetMilliseconds} ms under nominal load");
    }

    /// <summary>
    /// Shares one in-memory host across all probes so the cost of warming up
    /// the test server does not skew the p95 calculation.
    /// </summary>
    public sealed class PerformanceWebApplicationFactory : WebApplicationFactory<Program>
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

                string databaseName = $"changeorder-perf-{Guid.NewGuid():N}";
                services.AddDbContext<ApplicationDbContext>(opts =>
                    opts.UseInMemoryDatabase(databaseName));

                services.AddScoped<IUnitOfWork, InMemoryUnitOfWork>();
                services.AddScoped<IChangeOrderRepository, InMemoryChangeOrderRepository>();
            });
        }
    }
}
