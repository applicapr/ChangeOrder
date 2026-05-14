using System.Reflection;
using ChangeOrder.Business.Abstractions;
using ChangeOrder.Business.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChangeOrder.Business.Extensions;

/// <summary>Composition entry-point for the Business layer.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers every <see cref="ICommandHandler{TCommand, TResult}"/> and
    /// <see cref="IQueryHandler{TQuery, TResult}"/> implementation found in
    /// the Business assembly as a scoped service, plus the cross-cutting
    /// services (<c>OrderNumberGenerator</c>, <c>IdempotencyService</c>).
    /// Designed so that adding a new handler requires no change to the Host
    /// wiring.
    /// </summary>
    public static IServiceCollection AddBusinessLayer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        Assembly businessAssembly = typeof(ServiceCollectionExtensions).Assembly;
        IEnumerable<Type> concreteTypes = businessAssembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false });

        foreach (Type type in concreteTypes)
        {
            RegisterHandler(services, type, typeof(ICommandHandler<,>));
            RegisterHandler(services, type, typeof(IQueryHandler<,>));
        }

        services.AddScoped<OrderNumberGenerator>();
        services.AddScoped<IdempotencyService>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }

    private static void RegisterHandler(IServiceCollection services, Type implementation, Type openGeneric)
    {
        IEnumerable<Type> contracts = implementation
            .GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGeneric);

        foreach (Type contract in contracts)
        {
            services.AddScoped(contract, implementation);
        }
    }
}
