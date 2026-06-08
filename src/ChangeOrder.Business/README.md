# ChangeOrder.Business

Capa de lógica de negocio. Implementa el patrón CQRS (Command Query Responsibility
Segregation) separando las operaciones de escritura (Commands) de las de lectura
(Queries). Depende únicamente de ChangeOrder.Domain.

## Responsabilidad

Orquesta todas las operaciones del sistema — crear, actualizar, eliminar y consultar
órdenes de cambio. Cada operación tiene su propio Command/Query y Handler dedicado.

## Estructura

```
ChangeOrder.Business/
├── Abstractions/
│   ├── ICommandHandler.cs             # Contrato genérico para handlers de Commands
│   ├── IQueryHandler.cs               # Contrato genérico para handlers de Queries
│   └── IOrderNumberGenerator.cs       # Contrato para el generador de números de orden
├── Commands/
│   ├── CreateOrder/
│   │   ├── CreateOrderCommand.cs      # Datos para crear una orden
│   │   └── CreateOrderHandler.cs      # Lógica de creación
│   ├── UpdateOrder/
│   │   ├── UpdateOrderCommand.cs      # Datos para actualizar una orden
│   │   └── UpdateOrderHandler.cs      # Lógica de actualización
│   └── DeleteOrder/
│       ├── DeleteOrderCommand.cs      # Datos para eliminar una orden
│       └── DeleteOrderHandler.cs      # Lógica de borrado lógico
├── Queries/
│   ├── GetOrderById/
│   │   ├── GetOrderByIdQuery.cs       # Query por Id
│   │   └── GetOrderByIdHandler.cs     # Busca una orden por Id
│   ├── GetAllOrders/
│   │   ├── GetAllOrdersQuery.cs       # Query paginada
│   │   └── GetAllOrdersHandler.cs     # Lista todas las órdenes
│   └── GetOrdersByDate/
│       ├── GetOrdersByDateQuery.cs    # Query por fecha
│       └── GetOrdersByDateHandler.cs  # Lista órdenes de una fecha
├── Services/
│   └── OrderNumberGenerator.cs        # Genera el número yyyyMMdd-##
└── Extensions/
└── ServiceCollectionExtensions.cs     # Registro de handlers en DI
```
## Componentes

### CQRS — Commands vs Queries

| Tipo | Operación | Modifica BD |
|---|---|---|
| Command | CreateOrder | ✅ Sí |
| Command | UpdateOrder | ✅ Sí |
| Command | DeleteOrder | ✅ Sí (soft-delete) |
| Query | GetOrderById | ❌ No |
| Query | GetAllOrders | ❌ No |
| Query | GetOrdersByDate | ❌ No |

### Abstracciones genéricas

**`ICommandHandler<TCommand, TResponse>`** — contrato para todos los Commands:

```csharp
Task<Result<TResponse, Error>> HandleAsync(TCommand command, CancellationToken ct);
```

**`IQueryHandler<TQuery, TResponse>`** — contrato para todas las Queries:

```csharp
Task<Result<TResponse, Error>> HandleAsync(TQuery query, CancellationToken ct);
```

### Commands

| Handler | Descripción |
|---|---|
| `CreateOrderHandler` | Genera OrderNumber, crea la entidad y persiste |
| `UpdateOrderHandler` | Busca la orden y actualiza sus campos modificables |
| `DeleteOrderHandler` | Busca la orden y ejecuta soft-delete vía repositorio |

### Queries

| Handler | Descripción |
|---|---|
| `GetOrderByIdHandler` | Retorna una orden por Id o error si no existe |
| `GetAllOrdersHandler` | Retorna lista paginada de órdenes |
| `GetOrdersByDateHandler` | Retorna órdenes de una fecha específica |

### `OrderNumberGenerator`

Implementa `IOrderNumberGenerator`. Genera el número de orden con formato
`yyyyMMdd-##` consultando el próximo secuencial del día al repositorio:

```csharp
OrderNumber number = await _generator.GenerateAsync(date, ct);
// → "20260224-01"
```

### `Result<TValue, TError>`

Patrón que evita el uso de excepciones para flujo de negocio. Todo handler
retorna un `Result` de éxito o error:

```csharp
Result<Guid, Error>.Success(order.Id)
Result<Guid, Error>.Failure(DomainErrors.Order.NotFound)
```

### `ServiceCollectionExtensions`

Registra todos los handlers y servicios en el contenedor de DI:

- `ICommandHandler<CreateOrderCommand, Guid>` → `CreateOrderHandler`
- `ICommandHandler<UpdateOrderCommand, Guid>` → `UpdateOrderHandler`
- `ICommandHandler<DeleteOrderCommand, Guid>` → `DeleteOrderHandler`
- `IQueryHandler<GetOrderByIdQuery, ChangeOrderEntity>` → `GetOrderByIdHandler`
- `IQueryHandler<GetAllOrdersQuery, IReadOnlyList<ChangeOrderEntity>>` → `GetAllOrdersHandler`
- `IQueryHandler<GetOrdersByDateQuery, IReadOnlyList<ChangeOrderEntity>>` → `GetOrdersByDateHandler`
- `IOrderNumberGenerator` → `OrderNumberGenerator`

## Reglas

- Esta capa solo referencia `ChangeOrder.Domain`.
- Nunca usar excepciones para errores de negocio — siempre `Result<T, E>`.
- Nunca llamar `SaveChangesAsync` sin pasar por `IUnitOfWork`.
- Los Commands modifican estado — las Queries solo leen.
- Cada Command y Query tiene su propia carpeta con Handler dedicado.
