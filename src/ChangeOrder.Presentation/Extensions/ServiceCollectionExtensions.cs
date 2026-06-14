using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ChangeOrder.Presentation.Endpoints;
using Microsoft.AspNetCore.Builder;

namespace ChangeOrder.Presentation.Extensions;

/// <summary>
/// 
/// </summary>

public static class ServiceCollectionExtensions
{

    /// <summary>
    /// Extensiones de IServiceCollection para registrar los servicios de la capa Presentation
    /// </summary>

    public static IServiceCollection AddPresentationServices(
        this IServiceCollection services)
    {
        return services;
    }

    /// <summary>
    /// Registra los servicios de la capa Presentation en el contenedor de DI
    /// </summary>

    public static IEndpointRouteBuilder MapPresentationEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapChangeOrderEndpoints();
        return app;
    }
}
