# ChangeOrder.Data

Capa de acceso a datos. Implementa toda la infraestructura de persistencia
usando Entity Framework Core 10 con SQL Server. Depende únicamente de
ChangeOrder.Domain.

## Responsabilidad

Implementa los contratos definidos en Domain — repositorios, DbContext y
configuraciones de EF Core. Gestiona automáticamente la auditoría y el
borrado lógico mediante el AuditInterceptor.

## Estructura

```
ChangeOrder.Data/
├── Context/
│   └── ChangeOrderDbContext.cs         # DbContext principal — implementa IUnitOfWork
├── Configurations/
│   └── ChangeOrderConfiguration.cs     # Mapeo completo de la entidad a SQL
├── Interceptors/
│   └── AuditInterceptor.cs             # Auditoría y soft-delete automáticos
├── Repositories/
│   └── ChangeOrderRepository.cs        # Implementa IChangeOrderRepository
├── Migrations/                         # Generada automáticamente por EF Core
└── Extensions/
    └── ServiceCollectionExtensions.cs  # Registro de servicios en DI
```


## Componentes

### `ChangeOrderDbContext`

DbContext principal de EF Core. Representa la conexión con la base de datos
e implementa `IUnitOfWork` para gestionar transacciones atómicas. Aplica
todas las configuraciones del assembly automáticamente con
`ApplyConfigurationsFromAssembly`.

### `ChangeOrderConfiguration`

Mapea `ChangeOrderEntity` a la tabla `dbo.ChangeOrders`. Configura:

- Value Objects aplanados como columnas propias (`OwnsOne`)
- Enums almacenados como string (`HasConversion<string>()`)
- Índices: `OrderNumber` UNIQUE, `RequestDate`, `Status`, `IsDeleted`
- Soft-delete global con `HasQueryFilter(x => !x.IsDeleted)`

### `AuditInterceptor`

Interceptor de EF Core que se ejecuta automáticamente en cada
`SaveChangesAsync`. Gestiona:

- **Auditoría** — actualiza `UpdatedAt` en entidades modificadas
- **Soft-delete** — convierte `EntityState.Deleted` en `Modified`,
  activa `IsDeleted = true` y registra `DeletedAt`

### `ChangeOrderRepository`

Implementa `IChangeOrderRepository` del dominio. Métodos disponibles:

| Método | Descripción |
|---|---|
| `GetByIdAsync` | Busca por Id — retorna null si no existe o está soft-deleted |
| `GetByDateAsync` | Órdenes de una fecha específica |
| `GetNextSequenceForDateAsync` | Próximo secuencial del día para generar OrderNumber |
| `AddAsync` | Agrega al contexto sin guardar — lo maneja IUnitOfWork |
| `Update` | Marca como modificada en el ChangeTracker |
| `Delete` | Marca para borrado — AuditInterceptor convierte en soft-delete |

### `ServiceCollectionExtensions`

Registra todos los servicios en el contenedor de DI:

- `AuditInterceptor` → Singleton
- `ChangeOrderDbContext` → con SQL Server y AuditInterceptor
- `IChangeOrderRepository` → Scoped → `ChangeOrderRepository`
- `IUnitOfWork` → Scoped → `ChangeOrderDbContext`

## Paquetes NuGet

- `Microsoft.EntityFrameworkCore.SqlServer`
- `Microsoft.EntityFrameworkCore.Tools`

## Reglas

- Esta capa solo referencia `ChangeOrder.Domain`.
- Nunca llamar `SaveChangesAsync` desde el repositorio — lo maneja `IUnitOfWork`.
- Nunca borrar registros físicamente — siempre soft-delete vía `AuditInterceptor`.
- Las Migrations se generan con EF Core — nunca modificar la BD directamente.
