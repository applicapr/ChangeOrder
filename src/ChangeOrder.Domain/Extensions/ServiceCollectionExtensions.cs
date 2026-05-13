using Microsoft.Extensions.DependencyInjection;

namespace ChangeOrder.Domain.Extensions;

/// <summary>
/// Service-collection composition entry-point for the Domain layer.
/// </summary>
/// <remarks>
/// Domain has no runtime dependencies today; this method exists so the Host
/// pipeline can call <c>AddDomain()</c> uniformly alongside the other layers.
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers Domain services. Currently a no-op.</summary>
    public static IServiceCollection AddDomain(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
