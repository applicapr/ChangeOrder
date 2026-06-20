using ChangeOrder.Business.Abstractions;
using ChangeOrder.Business.Commands.CreateOrder;
using ChangeOrder.Business.Commands.DeleteOrder;
using ChangeOrder.Business.Commands.SetOrderDates;
using ChangeOrder.Business.Commands.UpdateOrder;
using ChangeOrder.Business.Queries.GetAllOrders;
using ChangeOrder.Business.Queries.GetOrderById;
using ChangeOrder.Business.Queries.GetOrdersByDate;
using ChangeOrder.Business.Services;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;


namespace ChangeOrder.Business.Extensions;

/// <summary>
/// Extensiones de IServiceCollection para registrar los servicios de la capa Business.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra los handlers de Commands y Queries en el contenedor de DI.
    /// </summary>
    public static IServiceCollection AddBusinessServices(
        this IServiceCollection services)
    {
        services.AddScoped<ICommandHandler<CreateOrderCommand, Guid>, CreateOrderHandler>();
        services.AddScoped<ICommandHandler<UpdateOrderCommand, Guid>, UpdateOrderHandler>();
        services.AddScoped<ICommandHandler<DeleteOrderCommand, Guid>, DeleteOrderHandler>();
        services.AddScoped<IQueryHandler<GetOrderByIdQuery, ChangeOrderEntity>, GetOrderByIdHandler>();
        services.AddScoped<IQueryHandler<GetAllOrdersQuery, IReadOnlyList<ChangeOrderEntity>>, GetAllOrdersHandler>();
        services.AddScoped<IQueryHandler<GetOrdersByDateQuery, IReadOnlyList<ChangeOrderEntity>>, GetOrdersByDateHandler>();
        services.AddScoped<IOrderNumberGenerator, OrderNumberGenerator>();
        services.AddScoped<ICommandHandler<SetOrderDatesCommand, Guid>, SetOrderDatesHandler>();
        return services;
    }
}
