# Tasks — Bootstrap ChangeOrder.Api

> Lista inicial de tareas de alto nivel para construir el sistema desde la rama `feature/jlara/bootstrap`. Cada checkbox debería expandirse a sub-tareas concretas cuando se procese con `/tasks` de GitHub Spec Kit.

## Fase 0 — Bootstrap de la solución

- [ ] `Directory.Build.props` raíz con `TargetFramework=net10.0`, `LangVersion=14`, `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `GenerateDocumentationFile=true`.
- [ ] `.editorconfig` con file-scoped namespaces como error, tipos explícitos, modificadores siempre visibles.
- [ ] `ChangeOrder.slnx` referenciando los 5 proyectos en `src/`.
- [ ] Crear `src/` y las 5 carpetas de proyectos con sus respectivos `.csproj` vacíos.
- [ ] Configurar `ProjectReference` siguiendo el grafo Onion (sin transitivos redundantes).
- [ ] Verificar `dotnet build` → 0 errors / 0 warnings.

## Fase 1 — CI/CD esqueleto

- [ ] `.github/workflows/ci.yml` con restore, build, test, validación de 500 líneas.
- [ ] `Dockerfile` multi-stage en `src/ChangeOrder.Host/`.
- [ ] Caché NuGet en el job de CI.
- [ ] (Opcional) Job de publish a registry interno.

## Fase 2 — Domain

- [ ] `Entities/ChangeOrderEntity.cs` (aggregate root con auditoría + soft delete).
- [ ] `ValueObjects/OrderNumber.cs` (sealed record, factory `Create(date, sequence)`, validación de formato).
- [ ] `ValueObjects/RequesterInfo.cs`.
- [ ] `ValueObjects/ApprovalChain.cs`.
- [ ] `Enums/OrderStatus.cs`, `Enums/ApprovalStatus.cs`.
- [ ] `Errors/Error.cs` (record `Code` + `Message`), `Errors/DomainErrors.cs` (catálogo estático).
- [ ] `Abstractions/IChangeOrderRepository.cs`, `Abstractions/IUnitOfWork.cs`, `Abstractions/ISoftDeletable.cs`, `Abstractions/IAuditable.cs`.

## Fase 3 — Data

- [ ] `Persistence/ApplicationDbContext.cs` con `DbSet<ChangeOrderEntity>`.
- [ ] `Configurations/ChangeOrderConfiguration.cs` (UNIQUE en `OrderNumber`, índices, query filter para soft delete).
- [ ] `Repositories/ChangeOrderRepository.cs` con `GetNextSequenceForDateAsync(date)` thread-safe.
- [ ] `Interceptors/AuditInterceptor.cs` (`SaveChangesInterceptor` para `CreatedAt`/`UpdatedAt`/soft delete).
- [ ] Migración inicial `InitialCreate`.
- [ ] `Extensions/ServiceCollectionExtensions.cs` registra `DbContext` + `Repository` + `UnitOfWork`.

## Fase 4 — Business (CQRS)

- [ ] `Abstractions/ICommandHandler.cs`, `Abstractions/IQueryHandler.cs`.
- [ ] `Services/OrderNumberGenerator.cs` (consulta máximo del día + 1, retry on UNIQUE violation).
- [ ] `Services/IdempotencyService.cs` — decisión previa: storage en tabla o cache.
- [ ] `Commands/CreateOrder/` — Command + Validator + Handler.
- [ ] `Commands/UpdateOrder/`.
- [ ] `Commands/DeleteOrder/`.
- [ ] `Queries/GetOrderById/`.
- [ ] `Queries/GetAllOrders/` con paginación.
- [ ] `Extensions/ServiceCollectionExtensions.cs`.

## Fase 5 — Presentation

- [ ] `DTOs/Requests/CreateOrderRequest.cs`, `DTOs/Requests/UpdateOrderRequest.cs`.
- [ ] `DTOs/Responses/OrderResponse.cs`, `DTOs/Responses/PagedResponse<T>.cs`.
- [ ] `Mappers/OrderMapper.cs` (métodos estáticos).
- [ ] `Endpoints/ChangeOrderEndpoints.cs` con `IEndpointRouteBuilder.MapChangeOrders()`.
- [ ] Middleware/filter de idempotencia (lee `Idempotency-Key`).
- [ ] `Extensions/ServiceCollectionExtensions.cs`.

## Fase 6 — Host

- [ ] `Program.cs` con DI: `AddDomain`, `AddBusiness`, `AddData`, `AddPresentation`, `AddOpenApi`, `AddValidation`, `AddRateLimiter`, `AddHealthChecks`.
- [ ] `appsettings.json` con connection string y Serilog config.
- [ ] Configurar OpenAPI con `<InterceptorsNamespaces>` en `.csproj` (gotcha de .NET 10).
- [ ] Mapear `/health` y los endpoints de change-orders.
- [ ] Variables de entorno `DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP2SUPPORT=false` + `DOTNET_SYSTEM_NET_DISABLEIPV6=1` documentadas en README local del Host.

## Fase 7 — Tests

- [ ] `tests/ChangeOrder.Domain.Tests/` (xUnit + FluentAssertions).
- [ ] `tests/ChangeOrder.Business.Tests/` (NSubstitute para repos).
- [ ] `tests/ChangeOrder.Data.Tests/` (in-memory provider o Testcontainers SQL).
- [ ] `tests/ChangeOrder.Presentation.Tests/` (`WebApplicationFactory`).
- [ ] Test de concurrencia: 100 `OrderNumber` simultáneos sin colisión.
- [ ] Test de idempotencia: mismo `Idempotency-Key` dos veces → mismo `Id` devuelto.

## Fase 8 — Validación final

- [ ] OpenAPI verificada vía Swagger UI.
- [ ] Build verde, tests verdes, 500-line check verde en CI.
- [ ] Verificación manual: crear orden, recorrer aprobaciones, soft delete, idempotencia, rate limit 429.
- [ ] PR a `main` con merge `--no-ff` y scope `feat(release): v0.1.0`.
