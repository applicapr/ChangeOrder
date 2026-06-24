using System.Net;
using System.Net.Http.Json;
using ChangeOrder.Presentation.Tests.Fixtures;
using FluentAssertions;

namespace ChangeOrder.Presentation.Tests.Endpoints;

/// <summary>
/// Tests de integración para el endpoint de versión.
/// </summary>
public sealed class VersionEndpointTests : IClassFixture<ChangeOrderApiFactory>
{
    private readonly ChangeOrderApiFactory _factory;

    /// <summary>
    /// Inicializa los tests con la factory compartida.
    /// </summary>
    public VersionEndpointTests(ChangeOrderApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Valida que el endpoint de versión retorna nombre, versión y ambiente.
    /// </summary>
    [Fact]
    public async Task GetVersion_Returns200WithNameVersionEnvironment()
    {
        // Arrange
        HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/version");
        VersionResponse? content = await response.Content.ReadFromJsonAsync<VersionResponse>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotBeNull();
        content!.Name.Should().Be("ChangeOrder.Host");
        content.Version.Should().NotBeNullOrWhiteSpace();
        content.Environment.Should().Be("Testing");
    }

    private sealed record VersionResponse(
        string Name,
        string Version,
        string Environment);
}
