# Constitución — ChangeOrder.Api

> Documento vivo. Cualquier decisión técnica o de producto que contradiga este documento debe primero modificarlo. Fuente derivada de `Docs/ChangeOrder.Api.Rules.md`, `Docs/ChangeOrder_DataModel.pdf`, `Docs/ChangeOrder_Programmer_Guide.pdf` y `README.md`.

## 1. Misión del producto

Construir una WebAPI interna que gestione el ciclo de vida completo de las **Órdenes de Cambio** sobre aplicaciones en producción — desde la solicitud inicial hasta el despliegue verificado — garantizando trazabilidad, aprobaciones jerárquicas y compliance.

## 2. Principios arquitectónicos

### P1 — Onion Architecture estricta
- Dependencias siempre hacia adentro: `Domain ← {Business, Data} ← Presentation ← Host`.
- `Domain` no referencia nada externo (ni EF Core, ni MediatR, ni `Microsoft.AspNetCore.*`).
- `Presentation` NO referencia `Data`; pasa siempre por `Business`.
- `Host` es el único Composition Root; toda la DI vive ahí.
- Sin `ProjectReference` transitivos redundantes.

### P2 — CQRS explícito
- Separación clara `Commands/` (escritura) y `Queries/` (lectura) dentro de `Business`.
- Un Handler = una responsabilidad.
- Interfaces: `ICommandHandler<TCommand, TResult>`, `IQueryHandler<TQuery, TResult>`.

### P3 — Result Pattern para errores de negocio
- `Result<TValue, TError>` para flujos esperables. Excepciones SOLO para inesperados (I/O, red, bug).
- Catálogo central `DomainErrors` (ej. `DomainErrors.Order.NotFound(id)`, `DomainErrors.Order.DuplicateNumber`, `DomainErrors.Order.InvalidDateRange`).

### P4 — Persistencia y auditoría
- EF Core 10 Code-First sobre SQL Server.
- Soft delete obligatorio: interfaces `ISoftDeletable`, `IAuditable`.
- `AuditInterceptor` (`SaveChangesInterceptor`) gestiona `CreatedAt`, `UpdatedAt`, `DeletedAt` automáticamente; sobre `Deleted` setea `IsDeleted=true`, `DeletedAt=UtcNow` y vuelve el estado a `Modified`.
- `HasQueryFilter` excluye registros borrados de todas las consultas por default.
- `OrderNumber` con **UNIQUE constraint** en BD — la BD es la fuente de verdad bajo concurrencia.

### P5 — Mappers manuales
- Métodos estáticos de extensión (`OrderMapper.ToResponse(entity)`).
- Un mapper por entidad. Sin lógica de negocio dentro de mappers.
- Prohibido: AutoMapper, Mapster, o cualquier mapper basado en reflection.

## 3. Estándares de código

- **.NET 10 / C# 14**, `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `GenerateDocumentationFile=true`.
- File-scoped namespaces (error en `.editorconfig`).
- Tipos explícitos (`var` solo cuando el tipo es evidente del literal).
- Primary constructors cuando aplique.
- Composition over inheritance — `sealed` por defecto en clases concretas.
- **Max 500 líneas** por archivo `.cs` (excluyendo auto-generados).
- **Max 3 parámetros** en constructores/métodos (records exentos).
- Un tipo por archivo; nombre de archivo coincide con el tipo.
- `CancellationToken` obligatorio en todo método `async`.
- `.ConfigureAwait(false)` en `Business` y `Data`; nunca en `Presentation`/`Host`.
- `StringComparison.Ordinal` (o `OrdinalIgnoreCase`) explícito en comparaciones de strings.

## 4. Estándares de operación

- **Logging Serilog** estructurado a consola + archivo (rolling diario). PROHIBIDO loggear datos sensibles (emails completos, info personal).
- Todo `catch` registra el `ex` completo a `ILogger`/`Serilog.Log`.
- **Health check** en `/health` valida conectividad SQL Server.
- **Rate limiting** built-in .NET 10, ventana fija, 100 req/min, devuelve `429 Too Many Requests`.
- **Idempotencia POST** via header `Idempotency-Key`.
- **Paginación obligatoria** en endpoints de listas (`PagedRequest`/`PagedResponse<T>`, page≥1, size∈[1..50]).
- **Versionado API** con `Asp.Versioning.Http`. Base URL: `/api/v1/change-orders`.
- **OpenAPI 3.1** generado automáticamente. XML doc comments en todo tipo público.

## 5. Estándares de testing

- **xUnit + FluentAssertions + NSubstitute**.
- Naming: `MetodoBajoPrueba_Escenario_ResultadoEsperado`.
- Tests por capa, en carpeta `tests/`.

## 6. Estándares de proceso

- **Conventional Commits con scope**: `feat(domain):`, `fix(data):`, `chore(host):`, `docs(readme):`, `refactor(business):`, `test(business):`.
- Ramas: `main` (protegida), `feature/<topic>`, `feature/<user>/<topic>`, `fix/<topic>`, `release/vX.Y.Z`.
- PR obligatorio a `main` con merge commit `--no-ff`.
- **Pre-commit checklist**: `dotnet format` → `dotnet build` (0 warnings) → `dotnet test` → verificación de app en vivo.

## 7. Deployment

- Docker on-premises (servidor interno corporativo).
- API no expuesta a internet pública.
- CORS configurado solo para hosts internos.
- CI/CD GitHub Actions valida build + tests + límite 500 líneas por `.cs`.

## 8. Política de IA en el repositorio

- Ningún artefacto de IA per-developer (memoria, instrucciones agentes, comandos locales) se commitea — ver `.gitignore`.
- **Excepción**: los artefactos del workflow **Spec-Driven Development con GitHub Spec Kit** son documentación de producto y SÍ se commitean (specs, plan, tasks, constitution).

## 9. Enmiendas

Modificar este documento requiere PR explícito con scope `docs(constitution):` y mención al razonamiento del cambio.
