# Análisis del Proyecto

- **Fecha del análisis:** 2026-07-06
- **Directorio analizado:** `/Users/jlara/source/repos/Applica/pasantia/ChangeOrder` (rama `fix-dev/openapi-vulnerability`)
- **Tecnologías identificadas:** .NET 10, C# 14, ASP.NET Core Minimal APIs, EF Core 10 (SQL Server), Serilog, OpenAPI 3.1 + Scalar, xUnit + FluentAssertions + NSubstitute, Docker (multi-stage)
- **Herramientas de validación ejecutadas:** `dotnet restore`, `dotnet build -c Release`, `dotnet test`, `dotnet format --verify-no-changes`, `dotnet list package --vulnerable --include-transitive`, `dotnet list package --outdated`, conteo de líneas por archivo (detalle en Anexo A)
- **Limitaciones:** la aplicación no se ejecutó contra SQL Server real (no hay base de datos disponible y la connection string vive en `appsettings.Development.json`, git-ignorado); la imagen Docker no se construyó; los tests de integración usan EF InMemory, que no ejercita índices únicos ni concurrencia optimista (detalle en Anexo C).

## 1. Resumen

ChangeOrder es una **Web API** (monolito por capas) para la gestión de órdenes de cambio ("Control de Órdenes de Cambio"): cuando un cliente solicita una modificación a una aplicación en producción, el sistema registra la solicitud y le asigna un número de orden autogenerado con formato `yyyyMMdd-##` (p. ej. `20260224-01`). Expone CRUD completo más operaciones de seguimiento (fechas de evaluación/entrega/despliegue) y una cadena de aprobación de 4 niveles. El README (en español) documenta una Fase 2 futura: un cliente WPF/MVVM que consumirá esta API.

Tecnologías principales: ASP.NET Core 10 con Minimal APIs, EF Core 10 sobre SQL Server (con soft-delete, auditoría por interceptor y concurrencia optimista por `RowVersion`), Serilog (consola + archivo diario), rate limiting fijo (100 req/min), idempotencia de creación por `IdempotencyKey` + hash SHA-256 del payload, health check de SQL Server y documentación OpenAPI con UI Scalar (solo en Development).

El propósito está claramente documentado en `README.md` y `Docs/ChangeOrder.Api.Rules.md` (rulebook del proyecto, 24 secciones).

## 2. Estructura del proyecto

La solución usa el formato nuevo `ChangeOrder.slnx` (XML), con 5 proyectos de producto en `src/` y 4 de tests en `tests/`, todos `net10.0`. `Directory.Build.props` fija `LangVersion` (C# 14), `Nullable`, `TreatWarningsAsErrors=true` y `GenerateDocumentationFile=true` para toda la solución.

| Ruta | Responsabilidad | Relación con otros componentes | Observaciones |
|---|---|---|---|
| `src/ChangeOrder.Domain` | Entidades, value objects, enums, contratos de repositorio/UoW, patrón `Result<TValue,TError>` y catálogo `DomainErrors` | Núcleo sin dependencias; todos dependen de él | Modelo anémico: sin invariantes ni transiciones de estado (ver ERR-006) |
| `src/ChangeOrder.Business` | CQRS: 5 commands + 3 queries con handlers propios (`ICommandHandler<,>`/`IQueryHandler<,>`, sin MediatR), validador estático, `OrderNumberGenerator`, `IdempotencyService` | Referencia solo a Domain | Sin ningún `ILogger` en la capa; validación cubre solo 3 de 13 campos (ERR-007) |
| `src/ChangeOrder.Data` | EF Core: `ChangeOrderDbContext` (implementa `IUnitOfWork`), configuraciones `IEntityTypeConfiguration<T>`, `ChangeOrderRepository`, `AuditInterceptor`, migraciones | Referencia solo a Domain | Contiene la raíz del error crítico ERR-001 (secuencia por `COUNT+1`) |
| `src/ChangeOrder.Presentation` | Endpoints Minimal API (`/api/v1/change-orders`), DTOs request/response, mapper manual | Referencia solo a Business | 8 endpoints; sin metadatos OpenAPI de respuestas |
| `src/ChangeOrder.Host` | Composition root: `Program.cs`, Serilog, CORS, rate limiter, compresión, health check, `/version`, `IdempotencyCleanupService` | Referencia a Data y Presentation; cablea todo vía `Add*Services()` | Dockerfile multi-stage; connection string requerida y ausente del repo (ERR-008) |
| `tests/ChangeOrder.{Domain,Business,Data,Presentation}.Tests` | 64 tests xUnit (16/22/11/15) unitarios + integración (`WebApplicationFactory` + EF InMemory) | Un proyecto de test por capa; Presentation.Tests referencia a Host | Verdes al 100 %, pero InMemory oculta concurrencia e índices únicos (Anexo C) |
| `Docs/` | Rulebook del proyecto, modelo de datos y guía del programador (PDF/DOCX) | Documentación | `ChangeOrder.Api.Rules.md` es la fuente normativa |
| `AGENTS.md`, `README.md`, `CHANGELOG.md` | Guías de contribución, setup y changelog | Documentación | Referencian un CI (`.github/workflows/ci.yml`) que **no existe** |

**Dirección de dependencias entre proyectos** (verificada en los `.csproj` y `using`): `Domain` ← `Business` ← `Presentation` ← `Host`; `Domain` ← `Data` ← `Host`. Sin referencias transitivas redundantes en `src/`.

**Flujo de ejecución:**

1. `src/ChangeOrder.Host/Program.cs` arranca: configura Serilog desde `appsettings.json` (consola + archivo `logs/log-.txt` diario).
2. Lee `ConnectionStrings:DefaultConnection` con operador `!` (si falta, el arranque falla en runtime sin mensaje claro — ERR-008).
3. Registra las capas en DI: `AddDataServices` (DbContext SqlServer + `AuditInterceptor` singleton + repositorio/UoW scoped) → `AddBusinessServices` (8 handlers + `OrderNumberGenerator`, todos scoped) → `AddPresentationServices` → `AddHostServices` (`IdempotencyCleanupService`). Añade health check de SQL Server, OpenAPI, CORS (`InternalNetwork`), rate limiter y compresión.
4. Pipeline HTTP: `UseResponseCompression` → `UseCors` → `UseRateLimiter` → endpoints. En Development se mapean además `/openapi/v1.json` y la UI Scalar (`/scalar/v1`).
5. Una petición a `/api/v1/change-orders` entra por `ChangeOrderEndpoints.cs`, que inyecta el handler específico (`ICommandHandler`/`IQueryHandler`) — no hay mediador.
6. El handler valida (solo en Create), genera el `OrderNumber` si aplica, y persiste vía `IChangeOrderRepository` + `IUnitOfWork` (ambos resuelven al mismo `ChangeOrderDbContext` del scope: orden + registro de idempotencia se guardan en una sola transacción).
7. `AuditInterceptor` intercepta `SaveChangesAsync`: asigna `UpdatedAt` y convierte deletes en soft-delete (`IsDeleted`/`DeletedAt`) con cascada a los owned types.
8. El `Result<T, Error>` vuelve al endpoint, que mapea a DTO (`OrderMapper`) y a código HTTP.
9. En segundo plano, `IdempotencyCleanupService` borra registros de idempotencia con más de 24 h, cada hora.

## 3. Arquitectura

**Patrón identificado:** Onion Architecture con CQRS artesanal (sin MediatR ni dispatcher), verificado en dependencias reales y no solo en nombres de carpetas: `Domain.csproj` no referencia nada, `Business.csproj` solo a Domain, `Presentation.csproj` solo a Business, y `Host` compone Data + Presentation. Grep de `using` confirma que Business no conoce EF Core.

**Patrones de diseño presentes:** Result pattern (`Result<TValue,TError>`), Repository + Unit of Work, value objects como owned types de EF, interceptor de auditoría (`SaveChangesInterceptor`), background service con `PeriodicTimer`, idempotencia con clave + hash de payload, concurrencia optimista con `RowVersion`.

**Inyección de dependencias:** cada capa expone `Extensions/ServiceCollectionExtensions.cs`; `Program.cs` solo invoca esos métodos. Lifetimes correctos en toda la solución: DbContext/repositorio/handlers scoped, `AuditInterceptor` singleton sin estado, `IUnitOfWork` resuelto a la misma instancia del DbContext del scope.

**Manejo de errores:** Result pattern para fallos de negocio y excepción de dominio (`ConcurrencyException`) para conflictos de concurrencia, traducida desde `DbUpdateConcurrencyException` en el DbContext para no filtrar EF Core hacia Business — decisión correcta. Sin embargo, la aplicación **no tiene manejador global de excepciones** (`UseExceptionHandler`/`ProblemDetails` ausentes), por lo que toda excepción no capturada llega como 500 crudo (ERR-009).

**Evaluación:**

- **Cohesión:** buena en general. Matiz: `IChangeOrderRepository` mezcla 11 métodos de órdenes e idempotencia en un solo contrato; `IdempotencyService` es en realidad una función de hash.
- **Acoplamiento:** bajo entre capas. Fuga a nivel de tipos: los queries de Business devuelven `ChangeOrderEntity` (agregado completo con `RowVersion`, `IsDeleted`) como `TResponse`, por lo que `ChangeOrderEndpoints.cs:10-12` importa `ChangeOrder.Domain.Entities/Enums/Errors`. Las respuestas de éxito sí pasan siempre por DTO; los cuerpos de error serializan el record `Error` de Domain (ERR-015).
- **Dependencias circulares:** no se detectaron.
- **Violaciones de responsabilidades:** la más relevante es la ausencia de invariantes en el dominio — la entidad `ChangeOrderEntity` tiene todos los setters públicos y cada handler decide sus propias reglas de transición, lo que produjo tres comportamientos inconsistentes entre sí (ERR-006).
- **Abstracciones:** no hay abstracciones prematuras; falta una abstracción importante — la máquina de estados de `OrderStatus`/`ApprovalChain`.

## 4. Revisión del código

Se revisaron los 5 proyectos de `src/` archivo por archivo y los 4 proyectos de `tests/`. Los archivos agrupados y el criterio de agrupación se indican al final de cada capa. Los problemas mayores se referencian por su ID de la sección 5 para no duplicar.

### 4.1 ChangeOrder.Domain

#### `src/ChangeOrder.Domain/Errors/Result.cs`

**Responsabilidad:** Result pattern genérico (`Success`/`Failure`, constructor privado, `sealed record`).

**Problemas encontrados:**
- Sin guardas de acceso: leer `Value` de un fallo devuelve `default` silenciosamente (`Guid.Empty` para `TValue = Guid`) en lugar de lanzar (`Result.cs:19`). Hoy los consumidores comprueban `IsSuccess` primero, pero el tipo no lo obliga. Riesgo potencial, gravedad Media.
- Documentación XML placeholder: seis bloques `/// <summary></summary>` vacíos (`Result.cs:3-38`). Posible mejora, Baja.

**Positivo:** construcción inválida imposible (factorías estáticas + ctor privado).

#### `src/ChangeOrder.Domain/Errors/DomainErrors.cs`

**Responsabilidad:** catálogo centralizado de errores (`Order.NotFound`, `AlreadyExists`, `ValidationFailed`, `ConcurrencyConflict`, `IdempotencyKeyConflict`).

**Problemas encontrados:**
- `Order.AlreadyExists` (`DomainErrors.cs:21`) es código muerto — grep sin consumidores. Es exactamente el error que debería devolverse cuando el índice único de `OrderNumber` rechaza un insert (ERR-001/ERR-002), pero nadie hace esa traducción. Error confirmado, Baja (el síntoma) / señal del gap de ERR-002.

#### `src/ChangeOrder.Domain/Errors/ConcurrencyException.cs`

**Responsabilidad:** excepción de dominio para conflictos de concurrencia optimista.

**Problemas encontrados:**
- No define constructores con `message`/`innerException`: al traducirla en `ChangeOrderDbContext.cs:51` se pierde la excepción original de EF (qué entidad, qué filas). Combinado con la ausencia de logging (ERR-016), el conflicto es invisible operacionalmente. Error confirmado, Baja.

#### `src/ChangeOrder.Domain/Entities/ChangeOrderEntity.cs`

**Responsabilidad:** aggregate root (21 propiedades: identidad, solicitud, aprobación, estado, fechas, auditoría, soft-delete, `RowVersion`).

**Problemas encontrados:**
- Modelo 100 % anémico: setters públicos en `Status`, `Approval`, fechas; ninguna invariante vive en la entidad. Es la causa raíz de ERR-006. Riesgo alto, Media.
- Clase sin `sealed` (`ChangeOrderEntity.cs:11`), única del proyecto. Posible mejora, Baja.
- Doc incoherente: `CreatedAt` documenta «asignado automáticamente por el AuditInterceptor» (`:109`) pero `CreateOrderHandler.cs:92` lo asigna manualmente. Baja.

**Positivo:** `required`/`init` en identidad, value objects, `IAuditable`/`ISoftDeletable` bien aplicadas.

#### `src/ChangeOrder.Domain/ValueObjects/OrderNumber.cs`

**Responsabilidad:** value object `yyyyMMdd-##` con factoría `Create(DateTime, int)`.

**Problemas encontrados:**
- `Create` no valida `sequence >= 1` ni `date != default`: `Create(default, 1)` produce `"00010101-01"` sin error — ruta real vía ERR-013. Con `sequence >= 100` el formato pasa a 3 dígitos rompiendo el contrato documentado `-##` (`OrderNumber.cs:26`). Riesgo potencial, Baja.

#### Archivos triviales de Domain (agrupados)

**Criterio:** contratos y datos sin lógica ejecutable (sin ramas ni efectos). `Errors/Error.cs` (record `Code`/`Message`), `ValueObjects/RequesterInfo.cs`, `ValueObjects/ApprovalChain.cs` (4 `ApprovalStatus`, sin comportamiento — no puede imponer el orden jerárquico que su doc promete), `Enums/*`, `Abstractions/{IAuditable, ISoftDeletable, IUnitOfWork, IChangeOrderRepository}.cs`, `Entities/IdempotencyRecord.cs` (inmutable, correcto). Typos menores en docs («aprovacion», «desicion», «sw 4 niveles»).

### 4.2 ChangeOrder.Business

#### `src/ChangeOrder.Business/Services/OrderNumberGenerator.cs`

**Responsabilidad:** genera el próximo número del día delegando en `GetNextSequenceForDateAsync`.

**Problemas encontrados:** el servicio en sí es mínimo y correcto; los defectos están en la fuente de la secuencia (ver ERR-001 y ERR-002, evidencia en `ChangeOrderRepository.cs:60-66`). Sin ningún test (ni unitario ni de integración).

#### `src/ChangeOrder.Business/Commands/CreateOrder/CreateOrderHandler.cs`

**Responsabilidad:** creación con idempotencia, generación de número y persistencia atómica.

**Problemas encontrados:**
- Ventana de carrera de idempotencia entre el check (`:55-56`) y el commit (`:110`) — ERR-010.
- `RequestDate` sin validar ni normalizar `DateTimeKind` — ERR-013.
- Detalle de validación descartado: `List<string> validationErrors` se calcula y se tira; el cliente recibe siempre el error genérico (`:48-50`). Error confirmado, Media.
- Hash de idempotencia débil: `BuildPayloadString` (`:119-123`) excluye `VersionScreenshotPath` (misma key + screenshot distinto se considera «mismo payload») y concatena con `|` sin escape (payloads distintos pueden producir el mismo hash). Serializar a JSON canónico resuelve ambos. Error confirmado, Baja.
- Comparación de hash sin `StringComparison` explícito (`:60`), contra la convención del proyecto. Baja.

**Positivo:** orden + registro de idempotencia en un solo `SaveChangesAsync` (diseño atómico correcto); `CancellationToken` en las 5 llamadas async; `UtcNow` uniforme.

#### `src/ChangeOrder.Business/Validation/CreateOrderValidator.cs`

**Responsabilidad:** validación estática de `CreateOrderCommand` (regex de email source-generated).

**Problemas encontrados:**
- Cobertura mínima: valida 3 de 13 propiedades. Sin validar: `WorkDescription`, `RequestDetails`, `Justification`, `RequiredAction`, `RequesterName`, `RequesterPosition`, `RequesterDepartment`, `RequestDate`, `IdempotencyKey`. Las longitudes máximas de BD (50/100/150/200/500, `ChangeOrderConfiguration.cs:37-59`) no se verifican → `SqlException` de truncamiento (500) con entrada que el validador aceptó. `IdempotencyKey = Guid.Empty` se acepta, con lo que todos los clientes que omitan la key colisionan en la misma PK. Ver ERR-007.
- Es el único validador: `UpdateOrderCommand` escribe los mismos campos sin ninguna validación (ERR-007).

**Positivo:** `[GeneratedRegex]` (sin costo runtime, sin backtracking peligroso), patrón acumulador correcto, estático y testeable (aunque sin test directo).

#### `src/ChangeOrder.Business/Commands/SetApproval/SetApprovalHandler.cs`

**Responsabilidad:** registra el veredicto de un nivel de la cadena de aprobación.

**Problemas encontrados:**
- `Enum.Parse<ApprovalStatus>(command.Verdict)` (`:44`) — ERR-003: excepción por flujo de negocio y persistencia de valores indefinidos.
- Nivel desconocido → éxito silencioso: `_ => order.Approval` (`:52`) — ERR-005.
- Sin máquina de estados: aprueba nivel 4 con nivel 1 pendiente, aprueba órdenes canceladas, nunca transiciona `OrderStatus` — ERR-006.
- Magic strings case-sensitive para los niveles (`"requester"`, `"departmentHead"`, `"itHead"`, `"programmingDivision"`, `:48-51`) sin constantes compartidas con Presentation. Baja.
- No captura `ConcurrencyException` — ERR-011.

#### `src/ChangeOrder.Business/Commands/SetOrderDates/SetOrderDatesHandler.cs`

**Responsabilidad:** actualiza fechas de seguimiento y dispara transiciones de estado.

**Problemas encontrados:**
- Transiciones por mera presencia de fecha, sin mirar el estado actual (`:45-55`): regresa `Deployed` → `InProgress`, revive órdenes canceladas, permite `Deployed` sin aprobación — parte de ERR-006.
- Sin coherencia temporal: `ProductionDeployDate` anterior a `RequestDate` se acepta. Error confirmado, Media (dentro de ERR-007).
- No captura `ConcurrencyException` — ERR-011.

#### `src/ChangeOrder.Business/Commands/UpdateOrder/UpdateOrderHandler.cs`

**Responsabilidad:** actualización general con concurrencia optimista (token del cliente).

**Problemas encontrados:**
- Sin validador (contraste con Create): strings vacíos o de 51+ caracteres llegan a BD — ERR-007. `order.Status = command.Status` (`:51`) acepta cualquier transición — ERR-006. Además escribe `DeliveryDate` (`:50`) sin la transición a `InProgress` que `SetOrderDatesHandler` sí aplica: dos endpoints con semánticas distintas para el mismo campo.
- `catch (ConcurrencyException)` sin log (`:58-61`) — ERR-016.
- Archivo completo indentado un nivel extra (50 de los 62 diagnósticos de `dotnet format`). Baja.
- `command.RowVersion` sin null/empty-check antes de `UpdateWithConcurrencyToken` (`:52`): un array vacío degrada a conflicto garantizado con mensaje engañoso. Riesgo potencial, Baja.

**Positivo:** es el único handler con el ciclo completo de concurrencia optimista bien implementado (token → catch → `Result.Failure(ConcurrencyConflict)`); es el patrón de referencia a replicar en los otros tres handlers de escritura.

#### `src/ChangeOrder.Business/Commands/DeleteOrder/DeleteOrderHandler.cs`

**Responsabilidad:** borrado lógico (load → not-found → `Delete` → save).

**Problemas encontrados:** sin regla de negocio (se borra una orden `Deployed` o en plena aprobación); cada delete alimenta el escenario de ERR-001. Sin catch de `ConcurrencyException` (ERR-011).

#### `src/ChangeOrder.Business/Queries/` (3 handlers)

**Responsabilidad:** lecturas (todas / por id / por fecha) envueltas en `Result`.

**Problemas encontrados:**
- `GetAllOrdersQuery` declara `(int Page, int PageSize)` pero `GetAllOrdersHandler.cs:32` los ignora y llama `GetAllAsync(ct)` — contrato público engañoso + lectura de la tabla completa. ERR-012 / PERF-001.
- Los tres devuelven `ChangeOrderEntity` como `TResponse` (fuga de tipo hacia Presentation, ver sección 3).

#### `src/ChangeOrder.Business/Services/IdempotencyService.cs`

**Responsabilidad:** hash SHA-256 hex del payload. Sin defectos (`SHA256.HashData` estático thread-safe, UTF-8 explícito). Matiz de nomenclatura: es una función de hash, no un «Service». Sin test directo.

#### `src/ChangeOrder.Business/Extensions/ServiceCollectionExtensions.cs`

**Responsabilidad:** registro DI de 8 handlers + generador, todo scoped (correcto). Registros manuales uno a uno: al crecer, alguien olvidará registrar un handler y el fallo será en runtime. Posible mejora, Baja.

#### Archivos triviales de Business (agrupados)

**Criterio:** records de datos y contratos sin lógica. `Abstractions/{ICommandHandler, IQueryHandler, IOrderNumberGenerator}.cs` (correctos; exigen `CancellationToken` en la firma), 6 records `*Command.cs`/`*Query.cs` (data carriers; sus gaps se reportan en los handlers).

**Verificaciones transversales de la capa:** `CancellationToken` propagado en los 8 handlers sin fugas; cero `DateTime.Now` (solo `UtcNow`); cero `.ConfigureAwait(false)` en toda la solución (contraviene la convención declarada para Business/Data); cero `ILogger` en Business (ERR-016); sin violaciones Onion.

### 4.3 ChangeOrder.Data

#### `src/ChangeOrder.Data/Context/ChangeOrderDbContext.cs`

**Responsabilidad:** DbContext + `IUnitOfWork`; traduce `DbUpdateConcurrencyException` → `ConcurrencyException` de dominio.

**Problemas encontrados:**
- El `catch` no loguea ni conserva la excepción original como `InnerException` (`:49-52`) — ERR-016.
- Solo se traduce la excepción de concurrencia: un `DbUpdateException` por violación del índice único de `OrderNumber` o de la PK de idempotencia se propaga sin traducir → 500 crudo (no hay manejador global, ERR-009). Riesgo alto, Alta — es la mitad del problema de ERR-001/ERR-002/ERR-010.

**Positivo:** `sealed`, primary constructor, `ApplyConfigurationsFromAssembly`; la traducción mantiene a Business libre de EF.

#### `src/ChangeOrder.Data/Configurations/ChangeOrderConfiguration.cs`

**Responsabilidad:** mapeo completo del agregado (owned types, enums como string, soft-delete, RowVersion).

**Problemas encontrados:**
- Índice único sobre `OrderNumber` (`:31`) **sin filtro por `IsDeleted`**, combinado con query filter global (`:90`): un registro soft-borrado reserva su número para siempre — pieza clave de ERR-001.
- Sin índice sobre `RequestDate`, usada por las consultas por fecha y por la secuencia — PERF-003.

**Positivo:** enums persistidos como string acotado; `HasQueryFilter(x => !x.IsDeleted)` sin duplicación manual (no hay fugas de borrados); `RowVersion` con `IsRowVersion()` correcto (confirmado en el snapshot).

#### `src/ChangeOrder.Data/Repositories/ChangeOrderRepository.cs`

**Responsabilidad:** implementación única de `IChangeOrderRepository` (CRUD + secuencia + idempotencia + limpieza).

**Problemas encontrados:**
- `GetNextSequenceForDateAsync` (`:60-66`): `COUNT + 1` sobre el DbSet filtrado por soft-delete, sin transacción ni tabla de secuencias — causa directa de ERR-001 (colisión determinista) y ERR-002 (carrera).
- `GetAllAsync` (`:40-43`): `ToListAsync()` de toda la tabla, sin `Skip/Take` — ERR-012 / PERF-001.
- Cero `AsNoTracking` en lecturas (grep en toda la solución) — PERF-002.
- Ventana de carrera de idempotencia (check en `:115-119` + insert posterior) — ERR-010.

**Positivo:** `UpdateWithConcurrencyToken` (`:93-100`) implementa correctamente el token de concurrencia del cliente (`OriginalValue` → `WHERE RowVersion = @client`); `DeleteOldIdempotencyRecordsAsync` usa `ExecuteDeleteAsync` (borrado en SQL, sin ChangeTracker).

#### `src/ChangeOrder.Data/Interceptors/AuditInterceptor.cs`

**Responsabilidad:** `UpdatedAt` en modificaciones y conversión `Deleted` → soft-delete con cascada a owned types.

**Problemas encontrados:**
- Solo sobreescribe `SavingChangesAsync` (`:20`): una llamada futura a `SaveChanges()` síncrono ejecutaría un **DELETE físico** sin pasar por el interceptor — ERR-014 (hoy no hay llamadas síncronas, verificado por grep).
- El soft-delete marca la entidad completa como `Modified` (`:44`): el UPDATE incluye todas las columnas — PERF-004.

**Positivo:** la cascada a owned types (`:51-57`) es correcta y necesaria; registro como singleton válido (sin estado ni dependencias scoped).

#### `src/ChangeOrder.Data/ChangeOrder.Data.csproj`

**Problemas encontrados:**
- Rutas absolutas de otra máquina (`C:\Users\jhein\...`) en `:22` y `:26`. Verificado estáticamente: **no rompen el build multiplataforma** (el `Remove` es no-op y el `Include` de archivo inexistente no lo consume ningún target), pero introducen comportamiento no determinista entre máquinas y son basura de Visual Studio a eliminar. Error confirmado, Baja.
- Drift de versiones EF Core: Data usa 10.0.8 mientras Host usa `Microsoft.EntityFrameworkCore.Design` 10.0.9 y el snapshot declara `ProductVersion 10.0.9` — consecuencia de no tener CPM (ver sección 8). Error confirmado, Media.

#### Resto de Data (agrupados)

**Criterio:** configuración menor y código generado. `Configurations/IdempotencyRecordConfiguration.cs` (PK = `Key` — el constraint que salva ERR-010 de duplicar datos; índice por `CreatedAt` para la limpieza; correcto), `Extensions/ServiceCollectionExtensions.cs` (lifetimes correctos; falta `EnableRetryOnFailure` — PERF-006), `Migrations/` (3 migraciones + snapshot, 1.027 líneas generadas — solo se usaron para inferir índices/constraints; nadie ejecuta `Database.Migrate()` al arranque, la migración es manual por `dotnet ef`).

### 4.4 ChangeOrder.Host

#### `src/ChangeOrder.Host/Program.cs`

**Responsabilidad:** composition root y pipeline HTTP (114 líneas).

**Problemas encontrados:**
- Connection string con `!` null-forgiving y sin fuente en el repo (`:17-18`) — ERR-008.
- Sin `UseExceptionHandler`/`AddProblemDetails`/`IExceptionHandler` — ERR-009.
- CORS hardcodeado: `policy.WithOrigins("http://localhost:5151") // ajustar según las IPs internas reales` (`:46`) — SEG-001.
- Rate limiter hardcodeado (100 req/min) y particionado por `RemoteIpAddress` sin `UseForwardedHeaders` (`:63-71`) — SEG-002. Aplica también a `/health` (un monitor con burst puede dejar los probes en 429).
- `/version` (`:98-109`) expone assembly, versión y nombre del entorno sin autenticación — SEG-004.
- `Asp.Versioning.Http` referenciado en el `.csproj` y jamás usado (el versionado es el prefijo fijo `/api/v1`) — código muerto. Error confirmado, Baja.
- Sin `UseSerilogRequestLogging()` (se pierde el log estructurado por request). Posible mejora, Baja.
- `AddResponseCompression(EnableForHttps = true)` sin mitigación BREACH — SEG-005.

**Positivo:** orden del pipeline correcto (compresión → CORS → rate limiter → endpoints); OpenAPI/Scalar solo en Development; `RetryAfter` en el 429; `public partial class Program` para tests de integración; Serilog desde configuración; en Producción el 500 va sin cuerpo (no fuga detalles internos).

#### `src/ChangeOrder.Host/BackgroundServices/IdempotencyCleanupService.cs`

**Responsabilidad:** purga registros de idempotencia > 24 h, cada hora.

**Problemas encontrados:** intervalo y retención hardcodeados (`:35`, `:57` — deberían ser `IOptions`); `PeriodicTimer` sin tick inicial (si la app se reinicia con frecuencia < 1 h, la tabla nunca se limpia). Riesgo potencial, Baja.

**Positivo:** servicio ejemplar en lo demás — `CreateAsyncScope` por tick (sin captura de scoped en singleton), `catch` que loguea con `LogError(ex, ...)` sin tumbar el host, cancelación de apagado limpia.

#### Configuración y Docker (agrupados)

- `appsettings.json`: sin `ConnectionStrings` (parte de ERR-008); Serilog correcto; sin secretos. `launchSettings.json`: sin datos sensibles (5151/7151), pero forzado a `<Content>` en el `.csproj` (`:13`) acaba copiado al publish/imagen Docker — artefacto de desarrollo en el artefacto de producción, Baja.
- `Dockerfile`: multi-stage correcto (sdk → aspnet, `EXPOSE 8080`), pero **sin directiva `USER`** (corre como root — SEG-003) y **sin `.dockerignore`** (el `COPY . .` arrastra `.git/`, `tests/`, `bin/`/`obj/` del host: builds lentos y caché invalidada — PERF-005).
- `.editorconfig` exige `end_of_line = crlf` mientras `.gitattributes` fuerza `eol=lf` para `*.cs`/`*.json`/`*.csproj`: cada guardado pelea con el checkout y `dotnet format` puede generar diffs de línea completa. Error confirmado, Baja.
- `.gitignore` ignora `*.txt` globalmente (cualquier `.txt` legítimo futuro quedará sin versionar silenciosamente). Riesgo potencial, Baja.

### 4.5 ChangeOrder.Presentation

#### `src/ChangeOrder.Presentation/Endpoints/ChangeOrderEndpoints.cs`

**Responsabilidad:** registra los 8 endpoints bajo `/api/v1/change-orders` (GET all, GET `{id}`, GET `date/{date}`, POST, PUT `{id}`, DELETE `{id}`, PATCH `{id}/dates`, PUT `{id}/approvals/{level}`).

**Elementos principales:**

| Elemento | Tipo | Responsabilidad | Observaciones |
|---|---|---|---|
| `MapChangeOrderEndpoints` | método de extensión estático | mapeo de rutas → handlers inyectados | endpoints delgados, sin lógica de negocio (correcto) |

**Problemas encontrados:**
- `Enum.Parse<OrderStatus>(request.Status)` (`:124`) — ERR-004: 500 provocable por el cliente.
- PUT mapea todo fallo a 404 (`:129-130`): un `ConcurrencyConflict` se reporta como «no existe» en vez de 409 — ERR-011/ERR-015.
- Conflicto de idempotencia → 422 compartido con `ValidationFailed`, indistinguibles (`:101-102`) — ERR-015.
- `page`/`pageSize` sin validación (acepta 0, negativos, 100000) y sin implementación real detrás (`:57-58`) — ERR-012.
- Cuerpos de error serializan el record `Error` de Domain en vez de `ProblemDetails` RFC 7807 (`:48,130,145,166,184`) — ERR-015.
- Cero metadatos OpenAPI: ningún `.Produces<T>()`/`.ProducesProblem()` (grep sin resultados); el documento generado no refleja los 201/204/404/422 reales. Error confirmado, Media.
- GET all y GET por fecha usan `result.Value!` sin comprobar `IsSuccess` (`:60-63`, `:72-75`): hoy esos handlers nunca fallan; si algún día devuelven `Failure`, es NRE. Riesgo potencial, Baja.
- `UpdateOrderRequest.Id` es campo muerto (el endpoint usa el `id` de ruta y no valida coincidencia). Baja.
- Location del 201 hardcodeado duplicando el prefijo (`:104`); preferible `CreatedAtRoute("GetOrderById", ...)`. Baja.

**Positivo:** semántica base de códigos correcta (201 + Location, 204 sin body, 404 en inexistente); la idempotencia está aplicada al endpoint correcto (POST create).

#### `src/ChangeOrder.Presentation/Mapper/OrderMapper.cs`

**Responsabilidad:** mapeo manual `ChangeOrderEntity` → `OrderResponse`.

**Problemas encontrados:**
- **Gap funcional:** `OrderResponse` expone solo 7 campos; `WorkDescription`, `RequestDetails`, `Justification`, `RequiredAction`, las 3 fechas de seguimiento, `UpdatedAt` y **toda la cadena `Approval`** no se exponen en ningún endpoint. Consecuencia: se pueden ESCRIBIR aprobaciones (`PUT {id}/approvals/{level}`) pero jamás LEERLAS por la API — no existe DTO de detalle. Error confirmado, Alta.
- Inconsistencia carpeta/namespace: carpeta `Mapper/`, namespace `ChangeOrder.Presentation.Mappers` (`:4`) — viola la regla «namespaces = carpetas físicas». Baja.

#### DTOs y extensiones (agrupados)

**Criterio:** records posicionales `sealed` sin lógica. `CreateOrderRequest`, `UpdateOrderRequest`, `SetOrderDatesRequest`, `SetApprovalRequest`, `OrderResponse`: carriers correctos. Hallazgos puntuales: `OrderListResponse` es **código muerto** (grep: solo su declaración) y es justamente el DTO con `TotalCount` que la paginación real necesitaría (Media); ningún DTO tiene validación de forma en ninguna capa salvo Create (ERR-007); docs XML intercambiados en `Extensions/ServiceCollectionExtensions.cs:8-10` (dice «registra servicios» donde mapea endpoints). El `.csproj` no declara `<FrameworkReference Include="Microsoft.AspNetCore.App">`: compila porque `Microsoft.AspNetCore.OpenApi` lo arrastra transitivo — quitar ese paquete rompería el build de forma no obvia (Baja).

### 4.6 Tests (4 proyectos, 64 tests — todos en verde)

**Cobertura existente:** los 8 handlers de Business (incluida concurrencia de Update con mock), `AuditInterceptor` (sus 2 responsabilidades), repositorio (soft-delete en GetById, secuencia, Add/Delete), value objects, `Result`, endpoints felices + 404 + idempotencia (misma key con mismo/distinto payload) + health + version. Estructura AAA explícita, verificación de interacciones con NSubstitute (`Received`/`DidNotReceive`), builders reutilizables, convención `Method_Scenario_ExpectedResult` cumplida en la gran mayoría.

**Problemas encontrados:**
- **Test que consagra un bug:** `HandleAsync_InvalidLevel_DoesNotChangeApproval` (`tests/ChangeOrder.Business.Tests/Commands/SetApprovalHandlerTests.cs:100-122`) asserta `IsSuccess == true` con nivel inválido — blinda ERR-005 contra su corrección. Error confirmado, Media.
- **Nombre de test engañoso:** `GetHealth_SqlServerRunning_Returns200Healthy` (`tests/.../HealthEndpointTests.cs:26`) — la fixture elimina todos los health checks (`ChangeOrderApiFactory.cs:59-60`), así que «Healthy» se devuelve con cero checks; el escenario del nombre no existe. Error confirmado, Media.
- **0 `[Theory]` en toda la suite:** casos parametrizables (4 niveles de aprobación, veredictos, límites de paginación, emails) duplicados como `[Fact]` o ausentes. Media.
- **Sin tests:** `OrderNumberGenerator` (cero), `CreateOrderValidator` (directo), `OrderMapper`, `IdempotencyService.ComputeHash` (directo), `IdempotencyCleanupService`, `UpdateWithConcurrencyToken` y la traducción `DbUpdateConcurrencyException → ConcurrencyException`; endpoints sin ejercitar: `GET date/{date}` por HTTP, 404 de PATCH dates y approvals, verdict inválido, niveles `itHead`/`programmingDivision`, header `Location` del 201, rate limiting (429), CORS.
- **EF InMemory oculta los dos errores más graves:** no aplica índices únicos (ERR-001/ERR-002 invisibles) ni `rowversion` (`UpdateWithConcurrencyToken` es no-op efectivo: el test 204 de PUT pasa enviando `Array.Empty<byte>()` — contra SQL Server real produciría conflicto → hoy 404). `ExecuteDeleteAsync` no está soportado por InMemory, así que la limpieza de idempotencia no es testeable con esta infraestructura. Ver Anexo C.
- Estado compartido controlado: una BD InMemory por clase de test con `ResetDatabaseAsync()` antes de cada test — sin contaminación hoy, frágil si alguien quita el reset. Baja.
- `FluentAssertions` 8.4.0: desde la v8 tiene licencia comercial (Xceed) — en contexto empresarial puede requerir licencia o downgrade a 7.x. Riesgo potencial, Media.
- `Business.Tests.csproj` referencia Business y Domain (Domain ya es transitivo) — contra la regla del repo de refs inmediatas sin redundancia. Baja.

## 5. Posibles errores

| ID | Gravedad | Tipo | Ubicación | Estado |
|---|---|---|---|---|
| ERR-001 | Crítica | Lógica / integridad | `ChangeOrderRepository.cs:60-66` + `ChangeOrderConfiguration.cs:31,90` | Confirmado |
| ERR-002 | Alta | Condición de carrera | `OrderNumberGenerator.cs:30` + `CreateOrderHandler.cs:110` | Probable |
| ERR-003 | Alta | Excepción no controlada / datos corruptos | `SetApprovalHandler.cs:44` | Confirmado |
| ERR-004 | Alta | Excepción no controlada | `ChangeOrderEndpoints.cs:124` | Confirmado |
| ERR-005 | Alta | Lógica (éxito silencioso) | `SetApprovalHandler.cs:52` | Confirmado |
| ERR-006 | Alta | Estados inconsistentes | `SetApprovalHandler.cs:46-53`, `SetOrderDatesHandler.cs:45-55`, `UpdateOrderHandler.cs:51` | Confirmado |
| ERR-007 | Alta | Validación insuficiente | `CreateOrderValidator.cs` + ausencia de validadores Update/Dates/Approval | Confirmado |
| ERR-008 | Alta | Configuración / arranque | `Program.cs:17-18` + `appsettings.json` | Confirmado |
| ERR-009 | Alta | Excepciones no controladas (global) | `Program.cs` (ausencia de `UseExceptionHandler`/ProblemDetails) | Probable |
| ERR-010 | Media | Condición de carrera (idempotencia) | `CreateOrderHandler.cs:55-110` + `ChangeOrderRepository.cs:115-119` | Probable |
| ERR-011 | Media | Excepción no controlada (concurrencia) | `SetApprovalHandler.cs`, `SetOrderDatesHandler.cs`, `DeleteOrderHandler.cs` (sin catch) | Probable |
| ERR-012 | Media | Contrato engañoso / carga completa | `GetAllOrdersHandler.cs:32` + `ChangeOrderRepository.cs:40-43` + `ChangeOrderEndpoints.cs:57-58` | Confirmado |
| ERR-013 | Media | Validación de fechas | `CreateOrderHandler.cs:67` + `OrderNumber.cs:26` | Confirmado |
| ERR-014 | Media | Pérdida de datos potencial | `AuditInterceptor.cs:20` (sin ruta síncrona) | Potencial |
| ERR-015 | Media | Códigos HTTP / contrato de error | `ChangeOrderEndpoints.cs:101-102,129-130` | Confirmado |
| ERR-016 | Media | Manejo de errores (logging) | `ChangeOrderDbContext.cs:49-52`, `UpdateOrderHandler.cs:58-61` | Confirmado |

### ERR-001: Colisión determinista de `OrderNumber` tras un soft-delete — bloquea la creación de órdenes el resto del día

**Ubicación:** `src/ChangeOrder.Data/Repositories/ChangeOrderRepository.cs:60-66`, `src/ChangeOrder.Data/Configurations/ChangeOrderConfiguration.cs:31,90`
**Gravedad:** Crítica
**Estado:** Confirmado

**Por qué ocurre:** la secuencia diaria se calcula como `COUNT + 1` sobre un DbSet con query filter global `!IsDeleted`, pero el índice único sobre `OrderNumber` (migración `InitialCreate.cs:51-55`, `unique: true` **sin filtro**) incluye las filas soft-borradas. Un delete decrementa el `COUNT` sin liberar el número en el índice.

**Consecuencias:** con órdenes `-01` y `-02` del día, borrar la `-01` hace que el próximo create genere `-02`, que ya existe → `DbUpdateException` no traducida → HTTP 500. Como el insert falla, el conteo no crece: **todos los creates de esa fecha fallan indefinidamente**. Un solo delete inutiliza la creación de órdenes del día.

**Cómo reproducirlo:** (1) crear dos órdenes con la misma `RequestDate`; (2) borrar la primera (`DELETE /api/v1/change-orders/{id}`); (3) crear una tercera con esa fecha → 500 permanente. Requiere SQL Server real: EF InMemory no aplica índices únicos, por eso ningún test lo detecta.

**Solución recomendada:** calcular la secuencia con `MAX(sufijo) + 1` usando `IgnoreQueryFilters()` (o una tabla de secuencias / `SEQUENCE` de SQL Server), y traducir la violación del índice único a `DomainErrors.Order.AlreadyExists` (hoy código muerto) con reintento.

**Evidencia:**
```csharp
int count = await _context.ChangeOrders
    .CountAsync(x => x.RequestDate.Date == date.Date, ct);  // filtrado por !IsDeleted
return count + 1;                                            // el índice único incluye borradas
```

### ERR-002: Carrera entre creates concurrentes — mismo `OrderNumber` para dos peticiones

**Ubicación:** `src/ChangeOrder.Business/Services/OrderNumberGenerator.cs:30` + `src/ChangeOrder.Business/Commands/CreateOrder/CreateOrderHandler.cs:110`
**Gravedad:** Alta
**Estado:** Probable

**Por qué ocurre:** patrón read-then-insert sin transacción serializable ni bloqueo: dos POST concurrentes del mismo día leen el mismo `count` y generan el mismo número.

**Consecuencias:** el índice único garantiza que no se persisten duplicados (bien), pero el perdedor recibe `DbUpdateException` sin traducir → 500, sin reintento ni error de negocio.

**Cómo reproducirlo:** dos POST simultáneos con la misma `RequestDate` contra SQL Server real. No determinista; probabilidad crece con la carga.

**Solución recomendada:** la misma de ERR-001 (secuencia atómica + traducción a `AlreadyExists` + retry en el handler).

**Evidencia:** `GenerateAsync` (`OrderNumberGenerator.cs:30`) y `SaveChangesAsync` (`CreateOrderHandler.cs:110`) sin ninguna coordinación entre ambos.

### ERR-003: `Enum.Parse` del veredicto de aprobación — 500 provocable y persistencia de valores indefinidos

**Ubicación:** `src/ChangeOrder.Business/Commands/SetApproval/SetApprovalHandler.cs:44`
**Gravedad:** Alta
**Estado:** Confirmado

**Por qué ocurre:** `Enum.Parse<ApprovalStatus>(command.Verdict)` (a) lanza `ArgumentException` con cualquier string no exacto (`"aprobado"`, `"approved"` en minúscula), y (b) acepta strings numéricos: `"7"` produce `(ApprovalStatus)7`, valor indefinido que EF persiste como `"7"` (conversión enum→string).

**Consecuencias:** 500 provocable por cualquier cliente; corrupción del dato de aprobación con valores fuera del enum.

**Cómo reproducirlo:** `PUT /api/v1/change-orders/{id}/approvals/requester` con body `{"verdict": "aprobado"}` → 500. Con `{"verdict": "7"}` → 204 y dato corrupto.

**Solución recomendada:** `Enum.TryParse(..., ignoreCase: true, out var v)` + `Enum.IsDefined(v)` → `Result.Failure` (error 400/422).

**Evidencia:** `ApprovalStatus verdict = Enum.Parse<ApprovalStatus>(command.Verdict);`

### ERR-004: `Enum.Parse<OrderStatus>` en el endpoint PUT — 500 con `Status` inválido

**Ubicación:** `src/ChangeOrder.Presentation/Endpoints/ChangeOrderEndpoints.cs:124`
**Gravedad:** Alta
**Estado:** Confirmado

**Por qué ocurre:** el body de PUT trae `Status` como string libre y el endpoint lo parsea sin `TryParse`.

**Consecuencias:** `ArgumentException` → 500 (sin manejador global, ERR-009) en lugar de 400.

**Cómo reproducirlo:** `PUT /api/v1/change-orders/{id}` con `{"status": "Inexistente", ...}` → 500.

**Solución recomendada:** `TryParse` + `IsDefined` → 400 con detalle del campo.

**Evidencia:** `Enum.Parse<OrderStatus>(request.Status)` en la lambda del PUT.

### ERR-005: Nivel de aprobación desconocido → éxito silencioso

**Ubicación:** `src/ChangeOrder.Business/Commands/SetApproval/SetApprovalHandler.cs:52`
**Gravedad:** Alta
**Estado:** Confirmado

**Por qué ocurre:** el switch de niveles termina en `_ => order.Approval`: un `level` no reconocido (`"ItHead"` con mayúscula, `"gerente"`) devuelve la cadena sin cambios, y el handler igualmente persiste y retorna `Success`.

**Consecuencias:** el cliente recibe 204 y cree que aprobó; no se aprobó nada. Existe además un test que blinda este comportamiento (`SetApprovalHandlerTests.cs:100-122`).

**Cómo reproducirlo:** `PUT /api/v1/change-orders/{id}/approvals/ItHead` (mayúscula inicial) con veredicto válido → 204 sin efecto.

**Solución recomendada:** conjunto cerrado de niveles (enum o constantes) y `Result.Failure` con un nuevo `DomainErrors.Order.InvalidApprovalLevel`; corregir el test.

**Evidencia:** `_ => order.Approval` seguido de `Update` + `SaveChangesAsync` + `Success(order.Id)`.

### ERR-006: Sin máquina de estados — transiciones libres e inconsistentes entre handlers

**Ubicación:** `SetApprovalHandler.cs:46-53`, `SetOrderDatesHandler.cs:45-55`, `UpdateOrderHandler.cs:51`
**Gravedad:** Alta
**Estado:** Confirmado

**Por qué ocurre:** `ChangeOrderEntity` es anémica (setters públicos, cero invariantes) y cada handler improvisa sus reglas: `SetApproval` no comprueba niveles previos ni `OrderStatus` (se aprueba nivel 4 con nivel 1 `Pending`; se aprueban órdenes `Cancelled`); con los 4 niveles aprobados, `Status` nunca transiciona; `SetOrderDates` dispara transiciones por mera presencia de fecha (regresa `Deployed` → `InProgress`, revive canceladas, permite `Deployed` sin aprobación); `UpdateOrder` acepta cualquier `Status` del cliente (`Deployed` → `Draft`).

**Consecuencias:** estados de negocio incoherentes persistidos; la cadena «jerárquica» de 4 niveles documentada no está implementada en ninguna parte.

**Cómo reproducirlo:** `PUT .../approvals/programmingDivision` sobre una orden recién creada → 204 (nivel 4 aprobado con los otros 3 pendientes).

**Solución recomendada:** mover las invariantes a la entidad (métodos `ApplyApproval(level, verdict)` y `TransitionTo(OrderStatus)` que validen y devuelvan `Result`), y que los handlers deleguen.

**Evidencia:** switch por nivel sin consultar los demás niveles ni `order.Status`; `order.Status = command.Status` sin validación.

### ERR-007: Validación de entrada incompleta — errores 500 de base de datos con entrada "válida"

**Ubicación:** `src/ChangeOrder.Business/Validation/CreateOrderValidator.cs` (único validador); `UpdateOrderHandler.cs:43-51` sin validador
**Gravedad:** Alta
**Estado:** Confirmado

**Por qué ocurre:** el validador de Create cubre 3 de 13 propiedades; las longitudes máximas configuradas en EF (50/100/150/200/500) no se verifican; `RequestDate` e `IdempotencyKey` (acepta `Guid.Empty`) no se validan; Update/SetDates/SetApproval no tienen validador alguno.

**Consecuencias:** un `ProductionVersion` de 51 caracteres pasa la validación y revienta en `SaveChangesAsync` con `SqlException` de truncamiento → 500 en vez de 400; strings vacíos se persisten; con `IdempotencyKey = Guid.Empty` todos los clientes que omitan la key colisionan entre sí (el segundo recibe el recurso del primero si el payload coincide, o `IdempotencyKeyConflict` si no).

**Cómo reproducirlo:** POST con `productionVersion` de 51 caracteres → 500 (requiere SQL Server real; InMemory ignora `HasMaxLength`).

**Solución recomendada:** completar `CreateOrderValidator` con longitudes alineadas a `ChangeOrderConfiguration`, rango razonable de `RequestDate`, `IdempotencyKey != Guid.Empty`; crear validadores para Update y SetDates (coherencia temporal entre fechas).

**Evidencia:** `CreateOrderValidator.Validate` solo comprueba `ProgramName` (requerido + ≤200), `ProductionVersion` (requerido) y `RequesterEmail` (regex).

### ERR-008: Connection string ausente + operador null-forgiving — crash de arranque sin mensaje útil

**Ubicación:** `src/ChangeOrder.Host/Program.cs:17-18`; `appsettings.json` sin sección `ConnectionStrings`
**Gravedad:** Alta
**Estado:** Confirmado

**Por qué ocurre:** `GetConnectionString("DefaultConnection")!` silencia el null; la única fuente es `appsettings.Development.json` (git-ignorado) o variables de entorno. El `.csproj` no tiene `UserSecretsId`.

**Consecuencias:** en un clon limpio o contenedor sin `ConnectionStrings__DefaultConnection`, el arranque lanza una excepción de argumento nulo desde el health check sin indicar qué configuración falta.

**Cómo reproducirlo:** clonar el repo y ejecutar `dotnet run --project src/ChangeOrder.Host` sin crear `appsettings.Development.json`.

**Solución recomendada:** `?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection no configurada")` y documentar la variable de entorno para Docker.

**Evidencia:** `string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;`

### ERR-009: Sin manejador global de excepciones ni ProblemDetails

**Ubicación:** `src/ChangeOrder.Host/Program.cs` (ausencia de `UseExceptionHandler`/`AddProblemDetails`)
**Gravedad:** Alta
**Estado:** Probable

**Por qué ocurre:** no hay ningún middleware de manejo de errores registrado.

**Consecuencias:** toda excepción alcanzable (ERR-001/002/003/004/010/011) produce 500 con cuerpo vacío en Producción — sin contrato de error para el cliente. No fuga detalles internos (eso está bien), pero tampoco hay log estructurado del contexto de la petición.

**Cómo reproducirlo:** cualquiera de las reproducciones de ERR-003/ERR-004.

**Solución recomendada:** `AddProblemDetails()` + `UseExceptionHandler()` (o un `IExceptionHandler`) que loguee la excepción completa y devuelva RFC 7807.

**Evidencia:** grep sin resultados de `UseExceptionHandler|ProblemDetails|IExceptionHandler` en `src/`.

### ERR-010: Carrera de idempotencia entre comprobación y guardado

**Ubicación:** `CreateOrderHandler.cs:55-110` + `ChangeOrderRepository.cs:115-119`
**Gravedad:** Media
**Estado:** Probable

**Por qué ocurre:** entre `GetIdempotencyRecordAsync` y el `SaveChangesAsync` no hay bloqueo: dos reintentos concurrentes con la misma key pasan ambos el check con `null`.

**Consecuencias:** la PK sobre `Key` garantiza atomicidad (no quedan órdenes duplicadas — bien), pero el segundo request recibe 500 por violación de PK en lugar del replay con el `ResourceId` original — exactamente el escenario (reintento por timeout) para el que existe la idempotencia.

**Cómo reproducirlo:** dos POST simultáneos con la misma `IdempotencyKey` contra SQL Server real. No determinista.

**Solución recomendada:** capturar la violación de PK (`DbUpdateException`), releer el registro y devolver el recurso original.

**Evidencia:** check en `:55-56`, persistencia en `:110`, sin manejo de la excepción de unicidad.

### ERR-011: `ConcurrencyException` sin capturar en 3 de 4 handlers de escritura

**Ubicación:** `SetApprovalHandler.cs`, `SetOrderDatesHandler.cs`, `DeleteOrderHandler.cs` (sin catch); mapeo incorrecto en `ChangeOrderEndpoints.cs:129-130`
**Gravedad:** Media
**Estado:** Probable

**Por qué ocurre:** `RowVersion` está bien configurado y el DbContext traduce la excepción, pero solo `UpdateOrderHandler` la captura y la convierte en `Result.Failure(ConcurrencyConflict)`. Los otros tres la propagan. Además, cuando Update sí la captura, el endpoint mapea el fallo a 404.

**Consecuencias:** dos aprobadores simultáneos de niveles distintos → el segundo recibe 500; en PUT, un conflicto real se reporta como «no existe» (404) en vez de 409.

**Cómo reproducirlo:** requiere SQL Server real (InMemory no genera `rowversion`); dos escrituras concurrentes sobre la misma orden.

**Solución recomendada:** replicar el patrón de `UpdateOrderHandler` en los otros tres y mapear `ConcurrencyConflict`/`IdempotencyKeyConflict` a 409 en los endpoints.

**Evidencia:** catch presente solo en `UpdateOrderHandler.cs:58-61`; `if (!result.IsSuccess) return Results.NotFound(result.Error);` en el PUT.

### ERR-012: Paginación declarada pero no implementada — carga de la tabla completa

**Ubicación:** `ChangeOrderEndpoints.cs:57-58` → `GetAllOrdersHandler.cs:32` → `ChangeOrderRepository.cs:40-43`
**Gravedad:** Media
**Estado:** Confirmado

**Por qué ocurre:** el endpoint acepta `page`/`pageSize` (sin validar), la query los transporta y el handler los ignora; `GetAllAsync` hace `ToListAsync()` sin `Skip/Take`.

**Consecuencias:** el contrato del API miente (el cliente pagina y recibe todo) y cada GET all cuesta O(tabla completa) — ver PERF-001. El README anuncia «paginación obligatoria».

**Cómo reproducirlo:** `GET /api/v1/change-orders?page=1&pageSize=5` con 20 órdenes → devuelve 20.

**Solución recomendada:** `Skip/Take` en SQL con clamp de parámetros (`page >= 1`, `1 <= pageSize <= 100`) y respuesta con `TotalCount` (el DTO `OrderListResponse`, hoy muerto, sirve exactamente para esto).

**Evidencia:** doc-comment del propio handler: «Pendiente: implementar paginación» (`GetAllOrdersHandler.cs:26`).

### ERR-013: `RequestDate` sin validar — órdenes `"00010101-01"` y secuencia diaria controlada por el cliente

**Ubicación:** `CreateOrderHandler.cs:67` + `OrderNumber.cs:26`
**Gravedad:** Media
**Estado:** Confirmado

**Por qué ocurre:** `RequestDate` omitido en el JSON deserializa a `default(DateTime)` y viaja sin validación hasta el generador; tampoco se normaliza `DateTimeKind` ni se acota el rango.

**Consecuencias:** número de orden `"00010101-01"` persistido; la parte-fecha del número la controla el cliente y puede divergir sin límite de `CreatedAt` (UTC del servidor); offsets de zona horaria pueden partir la secuencia diaria.

**Cómo reproducirlo:** POST válido sin el campo `requestDate` → 201 con número `00010101-01`.

**Solución recomendada:** validar rango razonable en el validador y normalizar a fecha UTC (o usar la fecha del servidor).

**Evidencia:** `_generator.GenerateAsync(command.RequestDate, ct)` sin comprobación previa; `OrderNumber.Create` sin guardas.

### ERR-014: `AuditInterceptor` no cubre la ruta síncrona — DELETE físico potencial

**Ubicación:** `src/ChangeOrder.Data/Interceptors/AuditInterceptor.cs:20`
**Gravedad:** Media
**Estado:** Potencial

**Por qué ocurre:** solo sobreescribe `SavingChangesAsync`. Hoy no existen llamadas a `SaveChanges()` síncrono (verificado por grep), por eso no es un error activo.

**Consecuencias:** cualquier código futuro (seeder, test, script) que llame la ruta síncrona ejecutará deletes físicos sin auditoría, violando silenciosamente la regla «nunca borrar físicamente».

**Cómo reproducirlo:** no reproducible con el código actual; se activaría con la primera llamada síncrona.

**Solución recomendada:** sobrescribir también `SavingChanges` delegando en un método común.

**Evidencia:** un solo override en el archivo.

### ERR-015: Códigos HTTP incorrectos y contrato de error no estándar

**Ubicación:** `ChangeOrderEndpoints.cs:101-102` (idempotencia → 422), `:129-130` (concurrencia → 404), `:48,130,145,166,184` (record `Error` de Domain como body)
**Gravedad:** Media
**Estado:** Confirmado

**Por qué ocurre:** los fallos no-NotFound se colapsan a 404/422; los cuerpos de error serializan `{code, message}` del Dominio en vez de `ProblemDetails` RFC 7807; `IdempotencyKeyConflict` y `ValidationFailed` comparten el 422 y son indistinguibles por status.

**Consecuencias:** el cliente no puede distinguir «no existe» de «reintenta con datos frescos» (409) ni «payload inválido» de «key reutilizada».

**Cómo reproducirlo:** POST con misma `IdempotencyKey` y payload distinto → 422 (debería 409).

**Solución recomendada:** mapa explícito código de error → status (409 para `ConcurrencyConflict` e `IdempotencyKeyConflict`) + `Results.Problem(...)`.

**Evidencia:** `return Results.UnprocessableEntity(result.Error);` compartido para ambos códigos.

### ERR-016: `catch` sin logging y pérdida de la excepción original

**Ubicación:** `ChangeOrderDbContext.cs:49-52`, `UpdateOrderHandler.cs:58-61`
**Gravedad:** Media
**Estado:** Confirmado

**Por qué ocurre:** ambos `catch` no registran nada (grep: cero `ILogger` en Business y en el DbContext); `ConcurrencyException` no acepta `innerException`, así que se pierde la información de EF (entidad, filas).

**Consecuencias:** los conflictos de concurrencia son invisibles en logs — imposible diagnosticar en producción. Viola la regla del proyecto «todo catch loguea la excepción completa».

**Cómo reproducirlo:** cualquier conflicto de RowVersion — nada aparece en `logs/`.

**Solución recomendada:** añadir constructores `(string, Exception)` a `ConcurrencyException`, encadenar la original y loguear en ambos catch.

**Evidencia:** `catch (DbUpdateConcurrencyException) { throw new ...ConcurrencyException(); }` sin log ni inner.

## 6. Problemas de rendimiento

| ID | Problema | Ubicación | Causa | Impacto | Gravedad | Solución |
|---|---|---|---|---|---|---|
| PERF-001 | GET all carga la tabla completa | `ChangeOrderRepository.cs:40-43` | `ToListAsync()` sin `Skip/Take` (ERR-012) | O(n) filas + 3 owned types por fila en cada request | Alta | Paginación real en SQL |
| PERF-002 | Lecturas con tracking innecesario | `ChangeOrderRepository.cs` (todas las queries; grep: 0 `AsNoTracking` en la solución) | Materialización con ChangeTracker en flujos de solo lectura | CPU/memoria por request de lectura | Media | `AsNoTracking()` en `GetAll`/`GetByDate` y en `GetById` cuando el flujo es lectura |
| PERF-003 | Consulta por fecha no sargable y sin índice | `ChangeOrderRepository.cs:62` (`RequestDate.Date == date.Date`) + sin índice sobre `RequestDate` | `CONVERT(date, ...)` impide usar índice; además no existe | Table scan por cada create (la secuencia) y por cada GET por fecha | Media | Comparar por rango (`>= date && < date+1`) + índice sobre `RequestDate` |
| PERF-004 | Soft-delete actualiza todas las columnas | `AuditInterceptor.cs:44` | Entidad completa marcada `Modified` | UPDATE innecesariamente ancho | Baja | Marcar solo `IsDeleted`/`DeletedAt` como modificadas |
| PERF-005 | Build Docker sin caché ni contexto filtrado | `src/ChangeOrder.Host/Dockerfile:7` + ausencia de `.dockerignore` | `COPY . .` antes del restore arrastra `.git/`, `tests/`, `bin/`, `obj/` | Builds lentos, caché invalidada en cada cambio | Baja | `.dockerignore` + copiar `*.csproj`/props primero, restore, luego el resto |
| PERF-006 | Sin resiliencia ante fallos transitorios de SQL | `Data/Extensions/ServiceCollectionExtensions.cs` (`UseSqlServer` sin opciones) | Falta `EnableRetryOnFailure` | Errores 500 por microcortes de red/BD en contenedor/cloud | Baja | `sqlOptions.EnableRetryOnFailure()` |

Escenario de manifestación de PERF-001/002/003: crece linealmente con el volumen de órdenes; con pocas filas (estado actual) es inapreciable. Efecto secundario de la solución de PERF-003: el cambio a rango modifica la semántica si `RequestDate` guarda hora — conviene decidir si la columna debe ser `date`.

## 7. Seguridad

No se encontraron secretos ni credenciales en el repositorio (búsqueda en todo el árbol): las únicas connection strings versionadas son ejemplos de documentación con `Trusted_Connection` (sin contraseña) apuntando a localhost. `dotnet list package --vulnerable --include-transitive` reporta **cero paquetes vulnerables** (la vulnerabilidad NU1903 de OpenAPI que motivó esta rama está resuelta).

| ID | Riesgo | Ubicación | Severidad | Evidencia | Mitigación |
|---|---|---|---|---|---|
| SEG-001 | CORS con origen hardcodeado (placeholder admitido en comentario) | `Program.cs:46` | Media | `WithOrigins("http://localhost:5151") // ajustar según las IPs internas reales` | Leer orígenes de `Cors:AllowedOrigins` por entorno; nunca recompilar para cambiar red |
| SEG-002 | Rate limiter inefectivo detrás de proxy | `Program.cs:63-71` | Media | Partición por `RemoteIpAddress` sin `UseForwardedHeaders`; tras un reverse proxy todos comparten un bucket de 100 req/min (DoS accidental); IP nula → partición `"unknown"` | `ForwardedHeadersOptions` + límites configurables |
| SEG-003 | Contenedor ejecuta como root | `src/ChangeOrder.Host/Dockerfile` | Media | Sin directiva `USER`; la imagen base .NET 10 provee `$APP_UID` | `USER $APP_UID` antes del `ENTRYPOINT` |
| SEG-004 | Endpoints de diagnóstico sin restricción | `Program.cs:96,98-109` | Baja | `/health` revela disponibilidad de la BD; `/version` expone assembly, versión y nombre del entorno | Restringir por red/auth si se publica fuera de red interna |
| SEG-005 | Compresión sobre HTTPS (BREACH/CRIME) | `Program.cs:53-56` | Baja | `EnableForHttps = true` explícito sin mitigación | Evaluar desactivarla o excluir respuestas con datos sensibles |
| SEG-006 | `TrustServerCertificate=true` en ejemplos de docs | `README.md:131`, `Docs/ChangeOrder.Api.Rules.md:855` | Baja | Copiado a producción deshabilita la validación TLS del SQL Server | Nota en docs indicando que es solo para desarrollo |
| SEG-007 | 500 provocables por entrada del cliente | Ver ERR-003, ERR-004, ERR-007 | Media | `Enum.Parse` y longitudes sin validar permiten a cualquier cliente generar excepciones a voluntad | Las soluciones de ERR-003/004/007 |

Condiciones necesarias: SEG-001/002 solo son explotables/dañinas al desplegar fuera de localhost; SEG-003 requiere un escape de contenedor para escalar. Ninguna vulnerabilidad de inyección SQL (EF Core parametriza todo; sin SQL crudo — verificado), ni XSS/CSRF aplicables (API sin vistas ni cookies de sesión).

## 8. Mantenibilidad

| Área | Evaluación | Evidencia | Mejora sugerida |
|---|---|---|---|
| Estructura y organización | Buena | Onion estricta verificada; CQRS por feature; un tipo por archivo; máximo 303 líneas/archivo (regla ≤500 cumplida) | — |
| Consistencia de estilo | Deficiente | `dotnet format --verify-no-changes` falla con 62 diagnósticos en 12 archivos (50 en `UpdateOrderHandler.cs`); conflicto `crlf` (.editorconfig) vs `lf` (.gitattributes) | Ejecutar `dotnet format`, unificar EOL, y añadir el check al CI |
| Gestión de dependencias | Deficiente | 9 proyectos con versiones inline, sin `Directory.Packages.props`; drift ya presente (EF 10.0.8 en Data vs 10.0.9 en Host/snapshot); `Asp.Versioning.Http` sin uso | Migrar a Central Package Management; eliminar paquetes muertos |
| Código muerto | Deficiente | `DomainErrors.Order.AlreadyExists`, `OrderListResponse`, `UpdateOrderRequest.Id`, `Asp.Versioning.Http`, rutas `C:\Users\jhein\...` en `Data.csproj` | Eliminar o, en el caso de `AlreadyExists` y `OrderListResponse`, darles el uso para el que existen (ERR-002, ERR-012) |
| Documentación | Aceptable | XML docs extensos en español y rulebook detallado; pero: summaries vacíos (`Result.cs`), docs intercambiados (`Presentation/Extensions`), README con puertos/Swagger/endpoints desactualizados, CI documentado inexistente | Sincronizar README/AGENTS.md con el código; completar placeholders |
| Testabilidad y cobertura | Aceptable | 64 tests verdes, AAA, mocks con verificación de interacciones; pero 0 `[Theory]`, componentes clave sin tests (`OrderNumberGenerator`, validador), un test consagra un bug (T2) y otro miente (health), e InMemory oculta los 2 errores más graves | SQLite in-memory o Testcontainers para índices/concurrencia; tests de los gaps listados en 4.6 |
| Configuración | Deficiente | CORS, rate limits, intervalo/retención de limpieza hardcodeados; connection string sin validación ni `UserSecretsId` | Mover a `appsettings`/`IOptions` con validación al arranque |
| Automatización (CI/CD) | Crítica | No existe `.github/` pese a que README, AGENTS.md y el rulebook describen un pipeline (restore→build→test→regla 500 líneas) | Crear el workflow real; sin CI, `TreatWarningsAsErrors` y los 64 tests solo protegen a quien los ejecuta localmente |
| Manejo de errores | Deficiente | Result pattern bien aplicado, pero: sin manejador global (ERR-009), catch sin logging (ERR-016), cero `ILogger` en Business, excepciones de BD sin traducir | Las soluciones de ERR-009/011/016 |

## 9. Recomendaciones

| Orden | Prioridad | Recomendación | Problema que resuelve | Beneficio esperado | Dificultad |
|---|---|---|---|---|---|
| 1 | Alta | Reemplazar `COUNT+1` por secuencia atómica (`MAX` con `IgnoreQueryFilters()` o `SEQUENCE` de SQL Server) y traducir la violación del índice único a `AlreadyExists` con reintento en `CreateOrderHandler` | ERR-001, ERR-002 | Elimina el bloqueo del día completo y los 500 bajo concurrencia | Media |
| 2 | Alta | Sustituir ambos `Enum.Parse` por `TryParse` + `IsDefined` con retorno 400, y cerrar el conjunto de niveles de aprobación (constantes/enum) devolviendo fallo en nivel desconocido; corregir el test `HandleAsync_InvalidLevel_DoesNotChangeApproval` | ERR-003, ERR-004, ERR-005, SEG-007 | Elimina 500 provocables, datos corruptos y éxitos silenciosos | Baja |
| 3 | Alta | Validar la connection string al arranque con mensaje explícito y añadir `UseExceptionHandler` + `AddProblemDetails` con logging | ERR-008, ERR-009 | Arranque diagnosticable y contrato de error estándar | Baja |
| 4 | Alta | Completar validadores: longitudes alineadas a la config EF, `RequestDate` acotada y normalizada, `IdempotencyKey != Guid.Empty`, validador de Update y coherencia temporal en SetDates | ERR-007, ERR-013 | 400 en vez de 500; datos consistentes | Media |
| 5 | Alta | Implementar la máquina de estados en `ChangeOrderEntity` (`ApplyApproval`, `TransitionTo`) y delegar desde los 3 handlers | ERR-006 | Integridad del flujo de aprobación (el propósito central del sistema) | Alta |
| 6 | Media | Capturar `ConcurrencyException` en SetApproval/SetDates/Delete (patrón de `UpdateOrderHandler`) y mapear `ConcurrencyConflict`/`IdempotencyKeyConflict` a 409 | ERR-011, ERR-015 | Conflictos distinguibles y sin 500 | Baja |
| 7 | Media | Implementar la paginación real (`Skip/Take` + clamp + `OrderListResponse` con `TotalCount`) y exponer un DTO de detalle con la cadena de aprobación | ERR-012, PERF-001, gap M2 | Contrato honesto; las aprobaciones se pueden leer | Media |
| 8 | Media | Capturar la violación de PK de idempotencia y devolver el recurso original (replay) | ERR-010 | La idempotencia cumple su propósito bajo reintentos concurrentes | Media |
| 9 | Media | Crear el CI real (`.github/workflows/ci.yml`): restore → build → `dotnet format --verify-no-changes` → test | Área CI/CD (Crítica en §8) | Las reglas del proyecto pasan a ser verificadas | Baja |
| 10 | Media | Migrar a Central Package Management (`Directory.Packages.props`) y alinear EF Core a una sola versión | Drift 10.0.8/10.0.9; regla CPM | Actualizaciones atómicas; sin NU1605 futuros | Baja |
| 11 | Media | Endurecer despliegue: `USER $APP_UID` + `.dockerignore` en Docker; CORS y rate limits desde configuración; `ForwardedHeadersOptions` | SEG-001, SEG-002, SEG-003, PERF-005 | Imagen no-root y límites efectivos detrás de proxy | Baja |
| 12 | Media | Añadir tests con SQLite in-memory o Testcontainers para índice único y RowVersion; tests de `OrderNumberGenerator` y `CreateOrderValidator`; adoptar `[Theory]` | Gaps de 4.6, Anexo C | Los dos errores más graves dejan de ser invisibles para la suite | Media |
| 13 | Baja | Ejecutar `dotnet format`; sobrescribir `SavingChanges` síncrono en el interceptor; logging en ambos `catch` + constructores con `innerException` en `ConcurrencyException` | Formato, ERR-014, ERR-016 | Higiene y diagnóstico | Baja |
| 14 | Baja | Limpieza: código muerto (§8), rutas `C:\Users\jhein\...`, namespace `Mappers` vs carpeta `Mapper/`, unificar EOL, `AsNoTracking`, índice sobre `RequestDate`, `EnableRetryOnFailure`, sincronizar README | Varios menores | Mantenibilidad general | Baja |

### Fase 1: Correcciones urgentes

Recomendaciones 1–3: la secuencia de `OrderNumber` (único defecto que puede detener la operación por completo), los `Enum.Parse` (500 provocables por cualquier cliente y corrupción de datos) y el arranque/manejo global de errores. Todas tienen reproducción concreta y solución acotada.

### Fase 2: Estabilidad y calidad

Recomendaciones 4–9 y 12: validación completa, máquina de estados, concurrencia e idempotencia bien traducidas a HTTP, paginación real, CI y los tests que hoy no pueden detectar los errores graves (SQLite/Testcontainers). Al cierre de esta fase, el flujo de aprobación — el corazón funcional del sistema — queda íntegro y verificado.

### Fase 3: Refactorización y escalabilidad

Recomendaciones 10, 11, 13 y 14: CPM, endurecimiento de Docker/configuración, rendimiento (AsNoTracking, índices, retry) y limpieza general. Ninguna bloquea la operación; todas reducen el costo de mantenimiento a futuro.

## 10. Conclusión

**Estado general:** proyecto joven con una base arquitectónica sólida — Onion verificada en dependencias reales, CQRS disciplinado, Result pattern consistente, soft-delete/auditoría por interceptor bien resueltos, idempotencia atómica y concurrencia optimista correctamente configurada en persistencia — pero con defectos funcionales serios concentrados en la lógica de negocio: la generación del número de orden (su función distintiva) falla de forma determinista tras un borrado, y la cadena de aprobación de 4 niveles (su segundo pilar funcional) no valida absolutamente ninguna regla del flujo que la documentación describe.

**Fortalezas principales:** separación de capas impecable, build limpio con warnings-como-errores, 64 tests verdes con buena estructura, cero paquetes vulnerables, cero secretos versionados, `CancellationToken` y `UtcNow` uniformes, y dos piezas ejemplares para replicar internamente (`UpdateOrderHandler` para concurrencia; `IdempotencyCleanupService` para background services).

**Debilidades principales:** validación de entrada mínima (3 de 13 campos, sin límites de longitud), modelo de dominio anémico que delegó las invariantes a handlers inconsistentes entre sí, manejo de errores incompleto (sin manejador global, catch sin logging, excepciones de BD sin traducir), y una suite de tests cuya infraestructura (EF InMemory) es ciega precisamente a los dos errores más graves.

**Riesgos más importantes:** ERR-001 (bloqueo de creación de órdenes el resto del día tras un solo delete — reproducible sin concurrencia) y ERR-003/004/005/006 (corrupción e inconsistencia del flujo de aprobación, con 500 provocables por cualquier cliente).

**Nivel de confianza:** alto para todo lo estático (código leído archivo por archivo, build/tests/format/auditoría ejecutados); medio para los comportamientos que requieren SQL Server real (ERR-001/002/010/011 están confirmados por análisis del código y de las migraciones, pero no ejecutados contra la base de datos — ver Anexo C).

**Cambios de mayor valor:** las recomendaciones 1 y 2 (dos archivos, riesgo acotado) eliminan el defecto crítico y los 500 provocables; la recomendación 5 (máquina de estados) es la inversión estructural que protege el propósito del sistema.

## Anexo A. Validaciones ejecutadas

| Comando | Resultado | Observaciones |
|---|---|---|
| `dotnet restore ChangeOrder.slnx` | ✅ Exitoso (exit 0) | Proyectos al día |
| `dotnet build ChangeOrder.slnx -c Release --no-restore` | ✅ Exitoso (exit 0) | 0 warnings, 0 errores, 9 proyectos (con `TreatWarningsAsErrors=true`) |
| `dotnet test ChangeOrder.slnx -c Release --no-build` | ✅ Exitoso (exit 0) | **64/64 tests pasados**: Domain 16, Business 22, Data 11, Presentation 15 |
| `dotnet format ChangeOrder.slnx --verify-no-changes` | ❌ Falla (exit 2) | **62 diagnósticos** en 12 archivos: 56 WHITESPACE (50 solo en `UpdateOrderHandler.cs`), 3 IMPORTS, 2 CHARSET (migraciones generadas), 1 IDE0008 |
| `dotnet list ChangeOrder.slnx package --vulnerable --include-transitive` | ✅ Exitoso | **Cero paquetes vulnerables** (NU1903 de OpenAPI resuelta en esta rama) |
| `dotnet list ChangeOrder.slnx package --outdated` | ✅ Exitoso | Única major disponible: `Microsoft.OpenApi` 2.7.5 → 3.8.0; resto menores/patch (EF 10.0.8→10.0.9, etc.) |
| Conteo de líneas `.cs` (sin `bin`/`obj`) | ✅ | 5.309 líneas totales; máximo 303 (`ChangeOrderEndpointTests.cs`) — regla de 500 líneas cumplida; Migrations (generadas): 1.027 líneas |
| `git log` / `git status` | ✅ | Rama `fix-dev/openapi-vulnerability`, árbol limpio (solo `.engram/` y `CLAUDE.md` sin trackear, previos al análisis) |

No se ejecutó la aplicación (`dotnet run`) por requerir SQL Server y una connection string no disponible; no se construyó la imagen Docker; no se aplicaron migraciones (no hay base de datos de trabajo). Ningún archivo del proyecto fue modificado; este análisis solo creó `ANALISIS.md`.

## Anexo B. Cobertura del análisis

| Área o ruta | Revisada | Nivel de detalle | Motivo de exclusión |
|---|---|---|---|
| `src/ChangeOrder.Domain` (15 archivos) | Sí | Archivo por archivo | — |
| `src/ChangeOrder.Business` (23 archivos) | Sí | Archivo por archivo | — |
| `src/ChangeOrder.Data` (código propio) | Sí | Archivo por archivo | — |
| `src/ChangeOrder.Data/Migrations/` | Parcial | Solo inferencia de índices/constraints | Código generado por EF (1.027 líneas) |
| `src/ChangeOrder.Presentation` | Sí | Archivo por archivo (DTOs agrupados como records triviales) | — |
| `src/ChangeOrder.Host` (incl. Dockerfile, appsettings, launchSettings) | Sí | Archivo por archivo | — |
| `tests/` (4 proyectos, 64 tests) | Sí | Inventario + calidad de asserts + fixtures | — |
| Raíz (`.slnx`, `Directory.Build.props`, `.editorconfig`, `.gitignore`, `.gitattributes`) | Sí | Completo | — |
| `README.md`, `AGENTS.md`, `CHANGELOG.md`, `Docs/*.md` | Sí | Contraste docs vs código | PDFs/DOCX de `Docs/` no analizados (binarios; existe equivalente en Markdown) |
| `bin/`, `obj/`, `.git/`, `.idea/`, `.engram/` | No | — | Artefactos de build, VCS e IDE sin lógica del proyecto |

## Anexo C. Hallazgos no verificables

- **Comportamiento contra SQL Server real:** no hay base de datos disponible ni connection string en el repo. ERR-001, ERR-002, ERR-010 y ERR-011 están confirmados por lectura del código y de las migraciones (índice único sin filtro, PK de idempotencia, `rowversion`), pero su manifestación HTTP exacta (500 vs otro código) no se ejecutó. EF InMemory — la infraestructura de los tests de integración — no aplica índices únicos, no genera `rowversion`, ignora `HasMaxLength` y no soporta `ExecuteDeleteAsync`, por lo que estos escenarios son estructuralmente invisibles para la suite actual.
- **`IdempotencyCleanupService` end-to-end:** no testeable con InMemory (usa `ExecuteDeleteAsync`); requeriría SQLite in-memory o Testcontainers.
- **Imagen Docker:** no se construyó ni ejecutó; los hallazgos del Dockerfile (root, sin `.dockerignore`) son estáticos.
- **Rate limiter detrás de reverse proxy (SEG-002):** el colapso a un bucket único depende de la topología de despliegue, no observable desde el repo.
- **Pipeline CI:** imposible de validar — el workflow documentado no existe en el repositorio.
- **Fixture con LocalDB (`ChangeOrderApiFactory.cs:33`):** la cadena LocalDB es Windows-only, pero los tests pasan en macOS porque la fixture sustituye el DbContext por InMemory y elimina los health checks; no se pudo verificar el comportamiento de la fixture en Windows con LocalDB real.
