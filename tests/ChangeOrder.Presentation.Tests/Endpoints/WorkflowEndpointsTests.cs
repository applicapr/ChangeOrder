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
/// End-to-end tests for the US2 workflow endpoints
/// (<c>PUT /api/v1/change-orders/{id}/approvals/{level}</c> and
/// <c>PATCH /api/v1/change-orders/{id}/dates</c>). Drives an order from Draft
/// to Deployed through the API, and exercises rejection variants that must
/// surface 409 Conflict.
/// </summary>
public sealed class WorkflowEndpointsTests : IClassFixture<WorkflowEndpointsTests.InMemoryWebApplicationFactory>
{
    private readonly InMemoryWebApplicationFactory _factory;

    public WorkflowEndpointsTests(InMemoryWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FullWorkflow_FromDraftToDeployed_Succeeds()
    {
        using HttpClient client = _factory.CreateClient();
        OrderResponse created = await CreateOrderAsync(client, "workflow-e2e-001");
        created.Status.Should().Be("Draft");

        await ApproveAsync(client, created.Id, "requester");
        await ApproveAsync(client, created.Id, "departmentHead");
        await ApproveAsync(client, created.Id, "itHead");

        // The fourth approval flips status to Approved.
        await ApproveAsync(client, created.Id, "programmingDivision");

        // Delivery date moves Approved → InProgress.
        using HttpResponseMessage deliveryResponse = await PatchDatesAsync(client, created.Id, new MilestoneDatesRequest(
            DeliveryDate: DateTime.UtcNow.AddHours(1),
            InitialEvaluationDate: null,
            ProductionDeployDate: null,
            PostDeployScreenshotPath: null));
        deliveryResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Production deploy date moves InProgress → Deployed.
        using HttpResponseMessage deployResponse = await PatchDatesAsync(client, created.Id, new MilestoneDatesRequest(
            DeliveryDate: null,
            InitialEvaluationDate: null,
            ProductionDeployDate: DateTime.UtcNow.AddHours(2),
            PostDeployScreenshotPath: "/blobs/after.png"));
        deployResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RecordApproval_OnMissingOrder_Returns404()
    {
        using HttpClient client = _factory.CreateClient();
        Guid missingId = Guid.NewGuid();
        using HttpRequestMessage request = new(HttpMethod.Put, $"/api/v1/change-orders/{missingId}/approvals/requester")
        {
            Content = JsonContent.Create(new ApprovalVerdictRequest("Approved"))
        };

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RecordApproval_WithUnknownLevel_Returns400()
    {
        using HttpClient client = _factory.CreateClient();
        OrderResponse created = await CreateOrderAsync(client, "workflow-e2e-bad-level");

        using HttpRequestMessage request = new(HttpMethod.Put, $"/api/v1/change-orders/{created.Id}/approvals/notALevel")
        {
            Content = JsonContent.Create(new ApprovalVerdictRequest("Approved"))
        };

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RecordApproval_WithUnknownVerdict_Returns400()
    {
        using HttpClient client = _factory.CreateClient();
        OrderResponse created = await CreateOrderAsync(client, "workflow-e2e-bad-verdict");

        using HttpRequestMessage request = new(HttpMethod.Put, $"/api/v1/change-orders/{created.Id}/approvals/requester")
        {
            Content = JsonContent.Create(new ApprovalVerdictRequest("Maybe"))
        };

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PatchDates_DeployBeforeDelivery_Returns409()
    {
        using HttpClient client = _factory.CreateClient();
        OrderResponse created = await CreateOrderAsync(client, "workflow-e2e-409");
        await ApproveAsync(client, created.Id, "requester");
        await ApproveAsync(client, created.Id, "departmentHead");
        await ApproveAsync(client, created.Id, "itHead");
        await ApproveAsync(client, created.Id, "programmingDivision");

        using HttpResponseMessage response = await PatchDatesAsync(client, created.Id, new MilestoneDatesRequest(
            DeliveryDate: null,
            InitialEvaluationDate: null,
            ProductionDeployDate: DateTime.UtcNow.AddHours(2),
            PostDeployScreenshotPath: null));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PatchDates_OnMissingOrder_Returns404()
    {
        using HttpClient client = _factory.CreateClient();
        Guid missingId = Guid.NewGuid();
        using HttpResponseMessage response = await PatchDatesAsync(client, missingId, new MilestoneDatesRequest(
            DeliveryDate: DateTime.UtcNow,
            InitialEvaluationDate: null,
            ProductionDeployDate: null,
            PostDeployScreenshotPath: null));

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

    private static async Task ApproveAsync(HttpClient client, Guid id, string level)
    {
        using HttpRequestMessage request = new(HttpMethod.Put, $"/api/v1/change-orders/{id}/approvals/{level}")
        {
            Content = JsonContent.Create(new ApprovalVerdictRequest("Approved"))
        };
        using HttpResponseMessage response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, $"approval {level} should succeed; got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private static async Task<HttpResponseMessage> PatchDatesAsync(HttpClient client, Guid id, MilestoneDatesRequest body)
    {
        HttpRequestMessage request = new(HttpMethod.Patch, $"/api/v1/change-orders/{id}/dates")
        {
            Content = JsonContent.Create(body)
        };
        return await client.SendAsync(request);
    }

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

                string databaseName = $"changeorder-workflow-{Guid.NewGuid():N}";
                services.AddDbContext<ApplicationDbContext>(opts =>
                    opts.UseInMemoryDatabase(databaseName));

                services.AddScoped<IUnitOfWork, InMemoryUnitOfWork>();
                services.AddScoped<IChangeOrderRepository, InMemoryChangeOrderRepository>();
            });
        }
    }
}
