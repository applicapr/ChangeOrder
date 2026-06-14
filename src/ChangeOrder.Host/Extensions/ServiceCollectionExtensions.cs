using Microsoft.Extensions.DependencyInjection;

namespace ChangeOrder.Host.Extensions;

/// <summary>
/// Extensiones de IServiceCollection para registrar configuraciones específicas del Host.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra servicios adicionales específicos del Host.
    /// </summary>
    public static IServiceCollection AddHostServices(
        this IServiceCollection services)
    {
        return services;
    }
}
