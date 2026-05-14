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
/// End-to-end tests for the US3 maintenance verbs against
/// <c>WebApplicationFactory&lt;Program&gt;</c>: list (paginated), get-by-id,
/// update (Draft only) and soft delete. The InMemory provider is layered in
/// place of SQL Server; concurrency tokens are still asserted to be non-empty
/// strings on the wire but rowversion drift is exercised by a dedicated
/// Testcontainers test (R-3).
/// </summary>
public sealed class MaintenanceEndpointsTests : IClassFixture<MaintenanceEndpointsTests.InMemoryWebApplicationFactory>
{
    private readonly InMemoryWebApplicationFactory _factory;

    public MaintenanceEndpointsTests(InMemoryWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetById_ExistingOrder_Returns200()
    {
        using HttpClient client = _factory.CreateClient();
        OrderResponse created = await CreateOrderAsync(client, "maintenance-get-001");

        using HttpResponseMessage response = await client.GetAsync($"/api/v1/change-orders/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        OrderResponse? payload = await response.Content.ReadFromJsonAsync<OrderResponse>();
        payload.Should().NotBeNull();
        payload!.Id.Should().Be(created.Id);
        // RowVersion is auto-populated by SQL Server in production; the InMemory provider used
        // by these end-to-end tests does not generate a token, so we only assert it is non-null.
        payload.RowVersion.Should().NotBeNull();
    }

    [Fact]
    public async Task GetById_MissingOrder_Returns404()
    {
        using HttpClient client = _factory.CreateClient();
        Guid missingId = Guid.NewGuid();

        using HttpResponseMessage response = await client.GetAsync($"/api/v1/change-orders/{missingId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_ReturnsPaginatedPayloadWithCreatedOrder()
    {
        using HttpClient client = _factory.CreateClient();
        OrderResponse created = await CreateOrderAsync(client, "maintenance-list-001");

        using HttpResponseMessage response = await client.GetAsync("/api/v1/change-orders?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        PagedOrderResponse? payload = await response.Content.ReadFromJsonAsync<PagedOrderResponse>();
        payload.Should().NotBeNull();
        payload!.Page.Should().Be(1);
        payload.PageSize.Should().Be(10);
        payload.TotalCount.Should().BeGreaterThanOrEqualTo(1);
        payload.Items.Should().Contain(o => o.Id == created.Id);
    }

    [Fact]
    public async Task List_InvalidPageSize_Returns400()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/v1/change-orders?page=1&pageSize=51");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_DraftOrder_Returns204AndPersistsChange()
    {
        using HttpClient client = _factory.CreateClient();
        OrderResponse created = await CreateOrderAsync(client, "maintenance-update-001");
        UpdateOrderRequest body = BuildUpdate(created.RowVersion, programName: "UpdatedApp");

        using HttpRequestMessage request = new(HttpMethod.Put, $"/api/v1/change-orders/{created.Id}")
        {
            Content = JsonContent.Create(body)
        };

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using HttpResponseMessage refetch = await client.GetAsync($"/api/v1/change-orders/{created.Id}");
        OrderResponse? refreshed = await refetch.Content.ReadFromJsonAsync<OrderResponse>();
        refreshed!.ProgramName.Should().Be("UpdatedApp");
    }

    [Fact]
    public async Task Update_OnNonDraftOrder_Returns409EditAfterDraft()
    {
        using HttpClient client = _factory.CreateClient();
        OrderResponse created = await CreateOrderAsync(client, "maintenance-update-409");

        // Advance the order out of Draft.
        using HttpRequestMessage approve = new(HttpMethod.Put, $"/api/v1/change-orders/{created.Id}/approvals/requester")
        {
            Content = JsonContent.Create(new ApprovalVerdictRequest("Approved"))
        };
        using HttpResponseMessage approved = await client.SendAsync(approve);
        approved.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Refetch to pick the new rowVersion (since we just modified the row).
        using HttpResponseMessage refetch = await client.GetAsync($"/api/v1/change-orders/{created.Id}");
        OrderResponse? refreshed = await refetch.Content.ReadFromJsonAsync<OrderResponse>();

        UpdateOrderRequest body = BuildUpdate(refreshed!.RowVersion, programName: "DoesNotMatter");
        using HttpRequestMessage request = new(HttpMethod.Put, $"/api/v1/change-orders/{created.Id}")
        {
            Content = JsonContent.Create(body)
        };

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_MissingOrder_Returns404()
    {
        using HttpClient client = _factory.CreateClient();
        Guid missingId = Guid.NewGuid();
        UpdateOrderRequest body = BuildUpdate(Convert.ToBase64String([0x01]), programName: "Whatever");

        using HttpRequestMessage request = new(HttpMethod.Put, $"/api/v1/change-orders/{missingId}")
        {
            Content = JsonContent.Create(body)
        };

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ExistingOrder_Returns204AndMakesItInvisibleToGet()
    {
        using HttpClient client = _factory.CreateClient();
        OrderResponse created = await CreateOrderAsync(client, "maintenance-delete-001");

        using HttpResponseMessage deleteResponse = await client.DeleteAsync($"/api/v1/change-orders/{created.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using HttpResponseMessage getResponse = await client.GetAsync($"/api/v1/change-orders/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_MissingOrder_Returns404()
    {
        using HttpClient client = _factory.CreateClient();
        Guid missingId = Guid.NewGuid();

        using HttpResponseMessage response = await client.DeleteAsync($"/api/v1/change-orders/{missingId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static async Task<OrderResponse> CreateOrderAsync(HttpClient client, string idempotencyKey)
    {
        CreateOrderRequest body = new(
            ProgramName: "BillingApp",
            ProductionVersion: "v1.0.0",
            VersionScreenshotPath: null,
            WorkDescription: "Fix rounding bug",
            RequestDetails: "Add half-even rounding to totals.",
            Justification: "Customer complaints about cents off.",
            RequiredAction: "Patch Module B, redeploy.",
            Requester: new RequesterInfoDto("Jane Doe", "QA Lead", "Quality", "jane.doe@example.com"));

        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/change-orders")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        using HttpResponseMessage response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        OrderResponse? payload = await response.Content.ReadFromJsonAsync<OrderResponse>();
        payload.Should().NotBeNull();
        return payload!;
    }

    private static UpdateOrderRequest BuildUpdate(string rowVersion, string programName) => new(
        ProgramName: programName,
        ProductionVersion: "v1.0.1",
        VersionScreenshotPath: null,
        WorkDescription: "Updated description",
        RequestDetails: "Updated details.",
        Justification: "Updated justification.",
        RequiredAction: "Updated action.",
        Requester: new RequesterInfoDto("Jane Doe", "QA Lead", "Quality", "jane.doe@example.com"),
        RowVersion: rowVersion);

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

                string databaseName = $"changeorder-maintenance-{Guid.NewGuid():N}";
                services.AddDbContext<ApplicationDbContext>(opts =>
                    opts.UseInMemoryDatabase(databaseName));

                services.AddScoped<IUnitOfWork, InMemoryUnitOfWork>();
                services.AddScoped<IChangeOrderRepository, InMemoryChangeOrderRepository>();
            });
        }
    }
}
