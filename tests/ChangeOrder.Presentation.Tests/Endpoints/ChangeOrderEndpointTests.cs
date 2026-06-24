using System.Net;
using System.Net.Http.Json;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Errors;
using ChangeOrder.Presentation.DTOs.Requests;
using ChangeOrder.Presentation.DTOs.Responses;
using ChangeOrder.Presentation.Tests.Fixtures;
using ChangeOrder.Presentation.Tests.Helpers;
using FluentAssertions;

namespace ChangeOrder.Presentation.Tests.Endpoints;

/// <summary>
/// Tests de integración para endpoints de órdenes de cambio.
/// </summary>
public sealed class ChangeOrderEndpointTests : IClassFixture<ChangeOrderApiFactory>, IAsyncLifetime
{
    private readonly ChangeOrderApiFactory _factory;
    private readonly HttpClient _client;

    /// <summary>
    /// Inicializa los tests con cliente HTTP real sobre WebApplicationFactory.
    /// </summary>
    public ChangeOrderEndpointTests(ChangeOrderApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Limpia la base de datos antes de cada test.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    /// <summary>
    /// No requiere limpieza adicional.
    /// </summary>
    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Valida que GET all retorna una lista HTTP 200.
    /// </summary>
    [Fact]
    public async Task GetAllOrders_Returns200WithList()
    {
        // Arrange
        await _factory.SeedOrderAsync(RequestBuilder.Order());

        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/v1/change-orders");
        IReadOnlyList<OrderResponse>? content =
            await response.Content.ReadFromJsonAsync<IReadOnlyList<OrderResponse>>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotBeNull();
        content!.Should().ContainSingle();
    }

    /// <summary>
    /// Valida que GET by id retorna la orden existente.
    /// </summary>
    [Fact]
    public async Task GetOrderById_ExistingId_Returns200WithOrder()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        await _factory.SeedOrderAsync(RequestBuilder.Order(id));

        // Act
        HttpResponseMessage response = await _client.GetAsync($"/api/v1/change-orders/{id}");
        OrderResponse? content = await response.Content.ReadFromJsonAsync<OrderResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotBeNull();
        content!.Id.Should().Be(id);
    }

    /// <summary>
    /// Valida que GET by id retorna 404 cuando la orden no existe.
    /// </summary>
    [Fact]
    public async Task GetOrderById_NonExistingId_Returns404ProblemDetails()
    {
        // Arrange
        Guid id = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await _client.GetAsync($"/api/v1/change-orders/{id}");
        Error? content = await response.Content.ReadFromJsonAsync<Error>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        content.Should().NotBeNull();
        content!.Code.Should().Be(DomainErrors.Order.NotFound.Code);
    }

    /// <summary>
    /// Valida que POST con request válido crea la orden.
    /// </summary>
    [Fact]
    public async Task CreateOrder_ValidRequest_Returns201WithId()
    {
        // Arrange
        CreateOrderRequest request = RequestBuilder.CreateOrderRequest();

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/change-orders", request);
        Guid id = await response.Content.ReadFromJsonAsync<Guid>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        id.Should().NotBeEmpty();
    }

    /// <summary>
    /// Valida que POST inválido retorna 422.
    /// </summary>
    [Fact]
    public async Task CreateOrder_InvalidRequest_Returns422ProblemDetails()
    {
        // Arrange
        CreateOrderRequest request = RequestBuilder.InvalidCreateOrderRequest();

        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/change-orders", request);
        Error? content = await response.Content.ReadFromJsonAsync<Error>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        content.Should().NotBeNull();
        content!.Code.Should().Be(DomainErrors.Order.ValidationFailed.Code);
    }

    /// <summary>
    /// Valida que POST idempotente con el mismo payload retorna el mismo id.
    /// </summary>
    [Fact]
    public async Task CreateOrder_SameIdempotencyKey_SamePayload_Returns201SameId()
    {
        // Arrange
        Guid idempotencyKey = Guid.NewGuid();
        CreateOrderRequest request = RequestBuilder.CreateOrderRequest(idempotencyKey);

        // Act
        HttpResponseMessage firstResponse = await _client.PostAsJsonAsync("/api/v1/change-orders", request);
        Guid firstId = await firstResponse.Content.ReadFromJsonAsync<Guid>();

        HttpResponseMessage secondResponse = await _client.PostAsJsonAsync("/api/v1/change-orders", request);
        Guid secondId = await secondResponse.Content.ReadFromJsonAsync<Guid>();

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        secondId.Should().Be(firstId);
    }

    /// <summary>
    /// Valida que POST idempotente con payload distinto retorna conflicto 422.
    /// </summary>
    [Fact]
    public async Task CreateOrder_SameIdempotencyKey_DifferentPayload_Returns422IdempotencyConflict()
    {
        // Arrange
        Guid idempotencyKey = Guid.NewGuid();
        CreateOrderRequest firstRequest = RequestBuilder.CreateOrderRequest(idempotencyKey);
        CreateOrderRequest secondRequest = firstRequest with { ProgramName = "DifferentApp" };

        // Act
        HttpResponseMessage firstResponse =
            await _client.PostAsJsonAsync("/api/v1/change-orders", firstRequest);

        HttpResponseMessage secondResponse =
            await _client.PostAsJsonAsync("/api/v1/change-orders", secondRequest);

        Error? content = await secondResponse.Content.ReadFromJsonAsync<Error>();

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        content.Should().NotBeNull();
        content!.Code.Should().Be(DomainErrors.Order.IdempotencyKeyConflict.Code);
    }

    /// <summary>
    /// Valida que PUT actualiza una orden existente.
    /// </summary>
    [Fact]
    public async Task UpdateOrder_ExistingId_Returns204()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        await _factory.SeedOrderAsync(RequestBuilder.Order(id));
        UpdateOrderRequest request = RequestBuilder.UpdateOrderRequest(id);

        // Act
        HttpResponseMessage response = await _client.PutAsJsonAsync($"/api/v1/change-orders/{id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Valida que PUT retorna 404 cuando la orden no existe.
    /// </summary>
    [Fact]
    public async Task UpdateOrder_NonExistingId_Returns404ProblemDetails()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        UpdateOrderRequest request = RequestBuilder.UpdateOrderRequest(id);

        // Act
        HttpResponseMessage response = await _client.PutAsJsonAsync($"/api/v1/change-orders/{id}", request);
        Error? content = await response.Content.ReadFromJsonAsync<Error>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        content.Should().NotBeNull();
        content!.Code.Should().Be(DomainErrors.Order.NotFound.Code);
    }

    /// <summary>
    /// Valida que DELETE elimina una orden existente.
    /// </summary>
    [Fact]
    public async Task DeleteOrder_ExistingId_Returns204()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        await _factory.SeedOrderAsync(RequestBuilder.Order(id));

        // Act
        HttpResponseMessage response = await _client.DeleteAsync($"/api/v1/change-orders/{id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Valida que DELETE retorna 404 cuando la orden no existe.
    /// </summary>
    [Fact]
    public async Task DeleteOrder_NonExistingId_Returns404ProblemDetails()
    {
        // Arrange
        Guid id = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await _client.DeleteAsync($"/api/v1/change-orders/{id}");
        Error? content = await response.Content.ReadFromJsonAsync<Error>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        content.Should().NotBeNull();
        content!.Code.Should().Be(DomainErrors.Order.NotFound.Code);
    }

    /// <summary>
    /// Valida que PATCH de fechas actualiza una orden existente.
    /// </summary>
    [Fact]
    public async Task SetOrderDates_ExistingId_Returns204()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        await _factory.SeedOrderAsync(RequestBuilder.Order(id));
        SetOrderDatesRequest request = RequestBuilder.SetOrderDatesRequest();

        // Act
        HttpResponseMessage response =
            await _client.PatchAsJsonAsync($"/api/v1/change-orders/{id}/dates", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Valida que PUT de aprobación actualiza una orden existente.
    /// </summary>
    [Fact]
    public async Task SetApproval_ExistingId_ValidLevel_Returns204()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        await _factory.SeedOrderAsync(RequestBuilder.Order(id));
        SetApprovalRequest request = RequestBuilder.SetApprovalRequest();

        // Act
        HttpResponseMessage response =
            await _client.PutAsJsonAsync($"/api/v1/change-orders/{id}/approvals/requester", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
