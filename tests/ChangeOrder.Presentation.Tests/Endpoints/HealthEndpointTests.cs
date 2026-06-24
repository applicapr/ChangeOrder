using System.Net;
using ChangeOrder.Presentation.Tests.Fixtures;
using FluentAssertions;

namespace ChangeOrder.Presentation.Tests.Endpoints;

/// <summary>
/// Tests de integración para endpoints de health.
/// </summary>
public sealed class HealthEndpointTests : IClassFixture<ChangeOrderApiFactory>
{
    private readonly ChangeOrderApiFactory _factory;

    /// <summary>
    /// Inicializa los tests con la factory compartida.
    /// </summary>
    public HealthEndpointTests(ChangeOrderApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Valida que el endpoint de health responde Healthy.
    /// </summary>
    [Fact]
    public async Task GetHealth_SqlServerRunning_Returns200Healthy()
    {
        // Arrange
        HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/health");
        string content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Be("Healthy");
    }
}
