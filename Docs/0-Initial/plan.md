# Plan técnico — ChangeOrder.Api

## 1. Stack

| Componente | Versión |
|---|---|
| .NET | 10 (LTS, soporte hasta 2028-11) |
| C# | 14 |
| Web framework | ASP.NET Core 10 — Minimal APIs |
| ORM | Entity Framework Core 10 — Code-First |
| Base de datos | SQL Server (MSSQL) |
| Logging | Serilog (Console + File sinks) |
| API docs | OpenAPI 3.1 / Swagger |
| Versionado API | Asp.Versioning.Http |
| Containers | Docker (multi-stage build) |
| CI/CD | GitHub Actions |

## 2. Arquitectura Onion

```
Domain  <--  Business  <--  Presentation  <--  Host (Composition Root)
   ^                                            |
   +----------  Data  --------------------------+
```

| Proyecto | Responsabilidad | Referencias |
|---|---|---|
| `ChangeOrder.Domain` | Entidades, value objects, enums, abstractions, errores | (ninguna) |
| `ChangeOrder.Business` | Commands, Queries, Handlers, Services | Domain |
| `ChangeOrder.Data` | DbContext, configurations, repositories, migrations, interceptors | Domain |
| `ChangeOrder.Presentation` | Endpoint groups, DTOs, mappers | Business |
| `ChangeOrder.Host` | Program.cs, DI, middleware, configuración | Presentation, Data |

## 3. Estructura por capa

### Domain
```
Entities/          — ChangeOrderEntity (aggregate root)
ValueObjects/      — OrderNumber, RequesterInfo, ApprovalChain
Enums/             — OrderStatus, ApprovalStatus
Errors/            — Error (record), DomainErrors (catálogo)
Abstractions/      — IChangeOrderRepository, IUnitOfWork, ISoftDeletable, IAuditable
```

### Business (CQRS)
```
Commands/
  CreateOrder/     — Command + Handler
  UpdateOrder/     — Command + Handler
  DeleteOrder/     — Command + Handler
Queries/
  GetOrderById/    — Query + Handler
  GetAllOrders/    — Query + Handler (paginado)
Services/          — OrderNumberGenerator, IdempotencyService
Abstractions/      — ICommandHandler<,>, IQueryHandler<,>
Extensions/        — ServiceCollectionExtensions
```

### Data
```
Persistence/       — ApplicationDbContext
Configurations/    — ChangeOrderConfiguration (EF Core fluent API)
Repositories/      — ChangeOrderRepository
Interceptors/      — AuditInterceptor (SaveChangesInterceptor)
Migrations/        — generadas por EF
Extensions/        — ServiceCollectionExtensions
```

### Presentation
```
Endpoints/         — ChangeOrderEndpoints (static, MapChangeOrders extension)
DTOs/              — Requests, Responses, PagedRequest, PagedResponse
Mappers/           — OrderMapper (estático)
Extensions/        — IEndpointRouteBuilder + ServiceCollectionExtensions
```

### Host
```
Program.cs         — WebApplication.CreateBuilder + DI completo
appsettings.json   — connection string + Serilog config
Dockerfile         — multi-stage build
```

## 4. Modelo de datos (resumen)

**Tabla `dbo.ChangeOrders`**:
- PK `Id` GUID (clustered).
- UNIQUE INDEX `IX_ChangeOrders_OrderNumber` en `OrderNumber` (varchar(13)).
- NONCLUSTERED INDEX en `RequestDate`, `Status`, `IsDeleted`.
- Soft delete: `IsDeleted` bit DEFAULT 0, `DeletedAt` datetime2 NULL.
- Auditoría: `CreatedAt` datetime2 NOT NULL (UTC), `UpdatedAt` datetime2 NULL.
- `RequesterInfo` desnormalizado en 4 columnas (Name, Position, Department, Email).
- `ApprovalChain` desnormalizado en 4 columnas (cada nivel con su `ApprovalStatus` varchar(20)).

## 5. Convenciones técnicas

- File-scoped namespaces.
- `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `GenerateDocumentationFile=true`.
- Tipos explícitos; `var` solo cuando el tipo es evidente.
- `CancellationToken` obligatorio en todo método `async`.
- `.ConfigureAwait(false)` en `Business`/`Data`.
- Result Pattern — sin excepciones para flujo de negocio.
- Mappers manuales (prohibido AutoMapper/Mapster).
- Validación vía `AddValidation()` built-in de .NET 10.
- `TypedResults` siempre (no `Results`).

## 6. CI/CD — `.github/workflows/ci.yml`

1. Restore con caché de NuGet.
2. Build (0 warnings; `TreatWarningsAsErrors=true`).
3. Test con coverage.
4. Validar que ningún `.cs` excede 500 líneas.
5. Publish artifact (opcional, según target de release).

Triggers: push a `main`, PR a `main`.

## 7. Deployment

- Imagen Docker multi-stage basada en `mcr.microsoft.com/dotnet/sdk:10` (build) y `mcr.microsoft.com/dotnet/aspnet:10` (runtime).
- Despliegue en servidor interno corporativo.
- Sin exposición a internet pública.
- Health probe Docker apunta a `/health`.

## 8. Decisiones técnicas pendientes

- **Idempotency-Key storage**: tabla `IdempotencyKeys` vs `IDistributedCache` (Redis vs in-memory).
- **Matriz de autorización por estado**: definir quién puede `Update`/`Delete` cuando la orden está `Approved` / `InProgress` / `Deployed`.
- **Validators**: dentro del Handler o como `IValidator<TCommand>` externo registrado en DI.
- **Retry policy** para `OrderNumber`: número de reintentos máximo + estrategia de backoff.

## 9. Gotcha de entorno conocido

`dotnet restore/build/run` falla contra `api.nuget.org` con `NU1301` por HTTP/2 ALPN roto en este entorno. **Workaround obligatorio**:
```bash
export DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP2SUPPORT=false
export DOTNET_SYSTEM_NET_DISABLEIPV6=1
```
Documentar en CI si el runner remoto presenta el mismo problema.
