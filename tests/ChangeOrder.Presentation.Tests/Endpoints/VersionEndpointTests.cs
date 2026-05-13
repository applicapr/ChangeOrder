using System.Net;
using System.Net.Http.Json;
using ChangeOrder.Host;
using ChangeOrder.Presentation.DTOs.Responses;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ChangeOrder.Presentation.Tests.Endpoints;

public sealed class VersionEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public VersionEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_Version_Returns200WithIdentityPayload()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/version", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        VersionResponse? payload = await response.Content.ReadFromJsonAsync<VersionResponse>();
        payload.Should().NotBeNull();
        payload!.Name.Should().Be("ChangeOrder.Api");
        payload.Version.Should().NotBeNullOrWhiteSpace();
        payload.Version.Should().NotContain("+", "the `+sha` build suffix must be stripped");
        payload.Environment.Should().NotBeNullOrWhiteSpace();
    }
}
