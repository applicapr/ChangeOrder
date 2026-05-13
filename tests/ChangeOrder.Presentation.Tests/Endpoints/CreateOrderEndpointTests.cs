using System.Net;
using System.Net.Http.Json;
using ChangeOrder.Data.Persistence;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Host;
using ChangeOrder.Presentation.DTOs.Requests;
using ChangeOrder.Presentation.DTOs.Responses;
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
/// End-to-end tests for <c>POST /api/v1/change-orders</c>. Uses an EF Core
/// InMemory database — the UPDLOCK + HOLDLOCK lock is not exercised here
/// (that is covered by the Testcontainers-backed concurrency test), but the
/// full request / response shape and idempotency control flow are validated.
/// </summary>
public sealed class CreateOrderEndpointTests : IClassFixture<CreateOrderEndpointTests.InMemoryWebApplicationFactory>
{
    private readonly InMemoryWebApplicationFactory _factory;

    public CreateOrderEndpointTests(InMemoryWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_WithValidBodyAndKey_Returns201()
    {
        using HttpClient client = _factory.CreateClient();
        CreateOrderRequest body = BuildRequest();
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/change-orders")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("Idempotency-Key", "endpoint-test-001");

        using HttpResponseMessage response = await client.SendAsync(request);

        string body500 = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.Created, $"body: {body500}");
        OrderResponse? payload = await response.Content.ReadFromJsonAsync<OrderResponse>();
        payload.Should().NotBeNull();
        payload!.OrderNumber.Should().MatchRegex(@"^\d{8}-\d{2}$");
        payload.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task Post_WithSameKeyAndSamePayload_ReturnsReplay200()
    {
        using HttpClient client = _factory.CreateClient();
        CreateOrderRequest body = BuildRequest();
        const string Key = "endpoint-test-replay";

        using HttpRequestMessage first = new(HttpMethod.Post, "/api/v1/change-orders")
        {
            Content = JsonContent.Create(body)
        };
        first.Headers.Add("Idempotency-Key", Key);
        using HttpResponseMessage firstResponse = await client.SendAsync(first);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        OrderResponse? firstPayload = await firstResponse.Content.ReadFromJsonAsync<OrderResponse>();

        using HttpRequestMessage second = new(HttpMethod.Post, "/api/v1/change-orders")
        {
            Content = JsonContent.Create(body)
        };
        second.Headers.Add("Idempotency-Key", Key);
        using HttpResponseMessage secondResponse = await client.SendAsync(second);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        OrderResponse? secondPayload = await secondResponse.Content.ReadFromJsonAsync<OrderResponse>();
        secondPayload!.Id.Should().Be(firstPayload!.Id);
    }

    [Fact]
    public async Task Post_WithSameKeyDifferentPayload_Returns422()
    {
        using HttpClient client = _factory.CreateClient();
        const string Key = "endpoint-test-divergent";
        CreateOrderRequest first = BuildRequest();
        CreateOrderRequest second = first with { ProgramName = "DivergentApp" };

        using HttpRequestMessage firstRequest = new(HttpMethod.Post, "/api/v1/change-orders")
        {
            Content = JsonContent.Create(first)
        };
        firstRequest.Headers.Add("Idempotency-Key", Key);
        using HttpResponseMessage firstResponse = await client.SendAsync(firstRequest);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using HttpRequestMessage secondRequest = new(HttpMethod.Post, "/api/v1/change-orders")
        {
            Content = JsonContent.Create(second)
        };
        secondRequest.Headers.Add("Idempotency-Key", Key);
        using HttpResponseMessage secondResponse = await client.SendAsync(secondRequest);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Post_WithInvalidPayload_Returns400()
    {
        using HttpClient client = _factory.CreateClient();
        CreateOrderRequest body = BuildRequest() with
        {
            Requester = new RequesterInfoDto("Jane Doe", "QA Lead", "Quality", "not-an-email")
        };

        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/change-orders")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("Idempotency-Key", "endpoint-test-bad");
        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static CreateOrderRequest BuildRequest() => new(
        ProgramName: "BillingApp",
        ProductionVersion: "v1.0.0",
        VersionScreenshotPath: null,
        WorkDescription: "Fix rounding bug",
        RequestDetails: "Add half-even rounding to totals.",
        Justification: "Customer complaints about cents off.",
        RequiredAction: "Patch Module B, redeploy.",
        Requester: new RequesterInfoDto("Jane Doe", "QA Lead", "Quality", "jane.doe@example.com"));

    public sealed class InMemoryWebApplicationFactory : WebApplicationFactory<Program>
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
                // Strip every EF Core / SqlServer registration installed by AddDataLayer
                // so we can layer the InMemory provider cleanly on top.
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

                string databaseName = $"changeorder-tests-{Guid.NewGuid():N}";
                services.AddDbContext<ApplicationDbContext>(opts =>
                    opts.UseInMemoryDatabase(databaseName));

                services.AddScoped<IUnitOfWork, InMemoryUnitOfWork>();
                services.AddScoped<IChangeOrderRepository, InMemoryChangeOrderRepository>();
            });
        }
    }
}
