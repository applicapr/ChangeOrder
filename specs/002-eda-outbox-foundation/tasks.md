# Tasks: EDA Outbox Foundation

**Change**: `eda-outbox-foundation`
**Folder**: `specs/002-eda-outbox-foundation/`
**Date**: 2026-05-19
**Status**: Draft (tasks phase)
**Sources of truth**: [`design.md`](./design.md) (D1–D9) y [`proposal.md`](./proposal.md). El ADR-0009 (`Proposed`) es inmutable.

> Cada tarea es atómica, accionable y verificable. El campo **Capa Onion** identifica el proyecto afectado; **Dependencias** lista los `T###` que deben quedar `done` antes; el **Acceptance criterion** es testeable; **Tamaño** sigue la escala `xs ≤ 30min`, `s 30min–2h`, `m 2h–4h`, `l > 4h`. Ninguna `l` queda sin partir.
>
> Las tres banderas que el design cerró (Q2 D9 `CorrelationId`, Q3 `ChangeOrderRejected` fuera, `ChangeOrderCancelled` first-class) ya están reflejadas en las tareas pertinentes — no se reabren.

---

## Convenciones de cumplimiento (aplican a TODAS las tareas)

- file-scoped namespaces, `LangVersion=14`, `Nullable=enable`, `TreatWarningsAsErrors=true`, `GenerateDocumentationFile=true`.
- Máx. 500 líneas por `.cs`, máx. 3 parámetros por método (records exentos), un tipo top-level por archivo (nombre archivo = nombre tipo).
- `.ConfigureAwait(false)` en todo `await` de `ChangeOrder.Business`, `ChangeOrder.Data` y los `BackgroundService` de `ChangeOrder.Host` (alineado con `IdempotencyCleanupService` existente).
- `StringComparison.Ordinal` en comparaciones internas; `OrdinalIgnoreCase` solo en lookups case-insensitive deliberados.
- `sealed` por defecto en clases concretas nuevas. Composición sobre herencia.
- Cada `catch` registra el `Exception` completo via `ILogger` (preferentemente `LoggerMessage` source generator). Sin swallows silenciosos.
- Domain sigue con **cero** `PackageReference` externos (validación de ADR-0001 + D8).
- Workaround NuGet HTTP/2 (`DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP2SUPPORT=false`, `DOTNET_SYSTEM_NET_DISABLEIPV6=1`) obligatorio antes de cualquier `dotnet restore/build/test`.

---

## Fase 1 — Domain (eventos + aggregate root + `LastStatusChangeAt`)

### T001 — Crear marker `IDomainEvent`

- **Capa Onion**: Domain
- **Archivos**:
  - Crear `src/ChangeOrder.Domain/Abstractions/IDomainEvent.cs`
- **Dependencias**: (ninguna)
- **Descripción**: Interfaz marker pública con propiedad `DateTime OccurredAtUtc { get; }` y XML-doc. Namespace `ChangeOrder.Domain.Abstractions`. Cero referencias externas.
- **Acceptance**: `dotnet build src/ChangeOrder.Domain/ChangeOrder.Domain.csproj` compila sin warnings; `ChangeOrder.Domain.csproj` no introduce nuevos `<PackageReference>`.
- **Tamaño**: `xs`

### T002 — Crear records de eventos de transición de aggregate

- **Capa Onion**: Domain
- **Archivos** (uno por archivo, namespace `ChangeOrder.Domain.Events`):
  - Crear `src/ChangeOrder.Domain/Events/ChangeOrderSubmittedForApproval.cs`
  - Crear `src/ChangeOrder.Domain/Events/ApprovalRecorded.cs`
  - Crear `src/ChangeOrder.Domain/Events/ChangeOrderFullyApproved.cs`
  - Crear `src/ChangeOrder.Domain/Events/MilestoneDatesUpdated.cs`
  - Crear `src/ChangeOrder.Domain/Events/ChangeOrderCancelled.cs`
- **Dependencias**: T001
- **Descripción**: Cinco `record` inmutables que implementan `IDomainEvent`. Campos exactos según design §5.2 (tabla). `MilestoneDatesUpdated` declara el enum local `MilestoneKind { Delivery, InitialEvaluation, ProductionDeploy }` en el mismo archivo (excepción explícita a "un tipo por archivo" porque el enum es parte del contrato del record y no se usa fuera). `ChangeOrderRejected` **NO** se crea (Q3 cerrada — design §5.2).
- **Acceptance**: `dotnet build` compila sin warnings; los records son `sealed` (records lo son por defecto cuando se declaran como `record`); inspección manual confirma que ningún archivo importa EF Core, MediatR ni `System.Text.Json`.
- **Tamaño**: `s`

### T003 — Crear record `OrderStaleEscalationDue`

- **Capa Onion**: Domain
- **Archivos**:
  - Crear `src/ChangeOrder.Domain/Events/OrderStaleEscalationDue.cs`
- **Dependencias**: T001
- **Descripción**: `record` con campos `OrderId`, `OrderNumber`, `LastStatusChangeAtUtc`, `ScanWindowStartUtc`, `OccurredAtUtc`. Implementa `IDomainEvent`. Separado de T002 porque lo emite `StaleOrderScanner` (no el aggregate) y eso justifica un archivo de PR independiente para review.
- **Acceptance**: `dotnet build` compila; `ScanWindowStartUtc` documentado como clave de idempotencia (D5).
- **Tamaño**: `xs`

### T004 — Aggregate root: colección `_domainEvents` + `DomainEvents` + `ClearDomainEvents()`

- **Capa Onion**: Domain
- **Archivos**:
  - Modificar `src/ChangeOrder.Domain/Entities/ChangeOrder.cs` (o ubicación equivalente del aggregate)
- **Dependencias**: T001
- **Descripción**: Agregar `private readonly List<IDomainEvent> _domainEvents = [];` y exponer `public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;` + `public void ClearDomainEvents() => _domainEvents.Clear();`. No emite eventos todavía (eso es T006). El método `ClearDomainEvents` será invocado por el drain en `UnitOfWork`.
- **Acceptance**: `dotnet build` compila; test de propiedad cubre que `DomainEvents` es vacío al construir y que `ClearDomainEvents` lo vacía cuando se agrega manualmente (cubierto luego en T028).
- **Tamaño**: `xs`

### T005 — Aggregate root: agregar columna de dominio `LastStatusChangeAt` (D1)

- **Capa Onion**: Domain
- **Archivos**:
  - Modificar `src/ChangeOrder.Domain/Entities/ChangeOrder.cs`
- **Dependencias**: T004
- **Descripción**: Agregar `public DateTime LastStatusChangeAt { get; private set; }`. Inicializar en ctor al mismo valor que `CreatedAt`. **No** lo toca el `AuditInterceptor` (D1) — solo se asigna desde dentro de los métodos de transición de estado (T006). Documentar en XML-doc que esta propiedad SOLO se actualiza en transiciones de `Status`, no en edits de contenido.
- **Acceptance**: `dotnet build` compila; tests existentes del aggregate siguen pasando (sin tocarlos); el setter es `private`.
- **Tamaño**: `xs`

### T006 — Aggregate root: emisión de eventos en transiciones + actualización de `LastStatusChangeAt`

- **Capa Onion**: Domain
- **Archivos**:
  - Modificar `src/ChangeOrder.Domain/Entities/ChangeOrder.cs`
- **Dependencias**: T002, T004, T005
- **Descripción**: Modificar cada método de transición existente para apender el evento correspondiente a `_domainEvents` **después** de la mutación exitosa y, cuando la transición cambia `Status`, también actualizar `LastStatusChangeAt = DateTime.UtcNow` (o vía `TimeProvider` si el aggregate lo tiene; mantener consistencia con el patrón actual del aggregate). Mapeo:
  - `ctor` (creación) → `ChangeOrderSubmittedForApproval` (status inicial es `PendingApproval`, se setea `LastStatusChangeAt`).
  - `SubmitForApproval` (si existe como método separado del ctor) → `ChangeOrderSubmittedForApproval` + `LastStatusChangeAt`.
  - `RecordApproval(level, verdict)` → SIEMPRE apende `ApprovalRecorded` (incluso si `verdict == Rejected`). Si el chain cierra (`AllApproved`), también apende `ChangeOrderFullyApproved` y actualiza `Status → Approved` + `LastStatusChangeAt`. **NO** se emite `ChangeOrderRejected` (Q3 cerrada).
  - `RecordDeliveryDate`, `RecordInitialEvaluationDate`, `RecordProductionDeploy` → cada uno apende `MilestoneDatesUpdated` con el `Kind` correspondiente. `RecordProductionDeploy` también cambia `Status` y por ende actualiza `LastStatusChangeAt`.
  - `Cancel` → apende `ChangeOrderCancelled` + actualiza `LastStatusChangeAt` (D1 + nota first-class del design §5.2).
  - Métodos de edit de contenido (`UpdateContent` y similares que NO mueven `Status`) → **NO** apenden evento y **NO** tocan `LastStatusChangeAt`.
- **Acceptance**: `dotnet build` compila sin warnings; tests existentes del aggregate siguen verdes; verificación inline (revisión manual durante PR) confirma que cada path actualiza `LastStatusChangeAt` SOLO en transiciones de `Status`.
- **Tamaño**: `m`

### T007 — Contrato Domain: `IOutboxRepository`

- **Capa Onion**: Domain
- **Archivos**:
  - Crear `src/ChangeOrder.Domain/Abstractions/IOutboxRepository.cs`
- **Dependencias**: T001
- **Descripción**: Interfaz pura de Domain (sin EF en firmas). Métodos según design §7.2:
  - `Task<IReadOnlyList<OutboxClaim>> ClaimPendingAsync(int batchSize, CancellationToken ct)` — devuelve filas claimed con UPDLOCK+READPAST. `OutboxClaim` es un `record` con `Id, EventType, Payload, CorrelationId, Attempts` (definido en el mismo archivo si es < 20 líneas, o en archivo aparte).
  - `Task MarkProcessedAsync(Guid id, CancellationToken ct)`
  - `Task RecordFailureAsync(Guid id, string lastError, DateTime nextRetryAtUtc, CancellationToken ct)`
  - `Task MarkDeadLetterAsync(Guid id, CancellationToken ct)`
  - `Task AppendAsync(IDomainEvent evt, string? correlationId, CancellationToken ct)` — usado por `StaleOrderScanner` para insertar eventos fuera del pipeline HTTP.
- **Acceptance**: `dotnet build` compila; no hay referencias a `Microsoft.EntityFrameworkCore.*` ni `Microsoft.Data.SqlClient` en el archivo.
- **Tamaño**: `s`

### T008 — Contrato Domain: extender `IChangeOrderRepository.ListStalePendingApprovalAsync`

- **Capa Onion**: Domain
- **Archivos**:
  - Modificar `src/ChangeOrder.Domain/Abstractions/IChangeOrderRepository.cs`
- **Dependencias**: (ninguna)
- **Descripción**: Declarar `Task<IReadOnlyList<Guid>> ListStalePendingApprovalAsync(DateTime thresholdUtc, int pageSize, int page, CancellationToken ct);`. Documentar que devuelve solo IDs (no aggregates) y respeta soft-delete (`IsDeleted = 0`).
- **Acceptance**: `dotnet build` compila; firma respeta la regla de máx. 3 parámetros (los 4 actuales son válidos para repositorios — recordá que el límite de 3 aplica a constructores/métodos de servicio, no a queries de repo; si el linter del proyecto se queja, usar un `record StaleQuery(DateTime ThresholdUtc, int PageSize, int Page)`).
- **Tamaño**: `xs`

---

## Fase 2 — Data (entidad Outbox + mapping + migración + drain + repos)

### T009 — Entidad EF `OutboxMessageEntity`

- **Capa Onion**: Data
- **Archivos**:
  - Crear `src/ChangeOrder.Data/Entities/OutboxMessageEntity.cs`
- **Dependencias**: T001
- **Descripción**: Clase sealed POCO con propiedades correspondientes a las columnas de design §4: `Id (Guid)`, `OccurredAtUtc (DateTime)`, `EventType (string)`, `Payload (string)`, `ProcessedAtUtc (DateTime?)`, `Attempts (int)`, `LastError (string?)`, `NextRetryAtUtc (DateTime?)`, `DeadLetteredAtUtc (DateTime?)`, `CorrelationId (string?)` máx. 64, `CreatedAt (DateTime)`. **NO** implementa `IAuditable` ni `ISoftDeletable` (design §4 nota). Ctor por defecto `public` (EF lo requiere) o ctor sin parámetros + `init` setters.
- **Acceptance**: `dotnet build` compila; archivo < 100 líneas; tipo `sealed`.
- **Tamaño**: `s`

### T010 — Mapping EF `OutboxMessageConfiguration`

- **Capa Onion**: Data
- **Archivos**:
  - Crear `src/ChangeOrder.Data/Configurations/OutboxMessageConfiguration.cs`
- **Dependencias**: T009
- **Descripción**: `IEntityTypeConfiguration<OutboxMessageEntity>` que produce el DDL exacto de design §4: PK clustered en `Id`, `Payload` como `nvarchar(max)`, `EventType` `nvarchar(256)`, `LastError` `nvarchar(max)`, `CorrelationId` `.HasMaxLength(64).IsRequired(false)`, default value `0` para `Attempts`. Configurar los dos índices:
  - `IX_OutboxMessages_Pending` sobre `OccurredAtUtc` con `INCLUDE (EventType, Attempts, NextRetryAtUtc)` y filtro `[ProcessedAtUtc] IS NULL AND [DeadLetteredAtUtc] IS NULL` via `.HasFilter(...)`.
  - `IX_OutboxMessages_EventType_DeadLettered` sobre `(EventType, DeadLetteredAtUtc)`.
- **Acceptance**: `dotnet build` compila; revisión manual confirma que `.HasFilter` está presente y los nombres de índices coinciden con design §4.
- **Tamaño**: `s`

### T011 — Mapping EF `ChangeOrderConfiguration` actualizado para `LastStatusChangeAt`

- **Capa Onion**: Data
- **Archivos**:
  - Modificar `src/ChangeOrder.Data/Configurations/ChangeOrderConfiguration.cs`
- **Dependencias**: T005
- **Descripción**: Agregar `.Property(o => o.LastStatusChangeAt).IsRequired()` y el índice filtrado:
  - `IX_ChangeOrders_PendingApproval_Stale` sobre `LastStatusChangeAt` con `INCLUDE (Id)` y filtro `[Status] = 'PendingApproval' AND [IsDeleted] = 0`.
- **Acceptance**: `dotnet build` compila; revisión confirma que `IsRequired()` está presente y el nombre del índice coincide con design §4.
- **Tamaño**: `xs`

### T012 — `ApplicationDbContext.DbSet<OutboxMessageEntity>`

- **Capa Onion**: Data
- **Archivos**:
  - Modificar `src/ChangeOrder.Data/Persistence/ApplicationDbContext.cs`
- **Dependencias**: T009, T010
- **Descripción**: Agregar `public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();` y `modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());` (o asegurar que el barrido por reflexión existente lo levante automáticamente). Verificar también que el cambio de `ChangeOrderConfiguration` de T011 entra al modelo.
- **Acceptance**: `dotnet build` compila; `dotnet ef dbcontext info --project src/ChangeOrder.Data --startup-project src/ChangeOrder.Host` muestra la nueva entidad.
- **Tamaño**: `xs`

### T013 — Migración EF `AddOutboxAndStaleTracking`

- **Capa Onion**: Data
- **Archivos**:
  - Crear `src/ChangeOrder.Data/Migrations/<timestamp>_AddOutboxAndStaleTracking.cs` (generado por EF)
  - Modificar `src/ChangeOrder.Data/Migrations/ApplicationDbContextModelSnapshot.cs` (auto-actualizado)
- **Dependencias**: T010, T011, T012
- **Descripción**: Ejecutar `dotnet ef migrations add AddOutboxAndStaleTracking --project src/ChangeOrder.Data --startup-project src/ChangeOrder.Host` con el workaround NuGet HTTP/2 activo. Revisar el archivo generado para confirmar que incluye:
  - `CREATE TABLE [dbo].[OutboxMessages]` con todas las columnas de design §4 (incluyendo `CorrelationId NVARCHAR(64) NULL`).
  - Los dos índices del paso T010.
  - `ALTER TABLE [dbo].[ChangeOrders] ADD [LastStatusChangeAt] DATETIME2(7) NULL;`.
  - Backfill `UPDATE [dbo].[ChangeOrders] SET [LastStatusChangeAt] = COALESCE([UpdatedAt], [CreatedAt]) WHERE [LastStatusChangeAt] IS NULL;` (inyectado manualmente con `migrationBuilder.Sql(...)` en el `Up`, porque EF no lo genera).
  - `ALTER TABLE ... ALTER COLUMN [LastStatusChangeAt] DATETIME2(7) NOT NULL;`.
  - Índice filtrado `IX_ChangeOrders_PendingApproval_Stale`.
  - `Down` que revierte limpio: drop de índices, drop de tabla, drop de columna.
- **Acceptance**: `dotnet ef database update --project src/ChangeOrder.Data --startup-project src/ChangeOrder.Host` aplica limpio contra SQL Server local; `dotnet ef migrations script <previous> AddOutboxAndStaleTracking` produce SQL idéntico al esperado de design §4; `Down` también ejecuta limpio.
- **Tamaño**: `m`

### T014 — Marker + payloads JSON en `Data/Outbox/Payloads/`

- **Capa Onion**: Data
- **Archivos**:
  - Crear `src/ChangeOrder.Data/Outbox/IOutboxPayload.cs`
  - Crear `src/ChangeOrder.Data/Outbox/Payloads/ChangeOrderSubmittedForApprovalPayload.cs`
  - Crear `src/ChangeOrder.Data/Outbox/Payloads/ApprovalRecordedPayload.cs`
  - Crear `src/ChangeOrder.Data/Outbox/Payloads/ChangeOrderFullyApprovedPayload.cs`
  - Crear `src/ChangeOrder.Data/Outbox/Payloads/MilestoneDatesUpdatedPayload.cs`
  - Crear `src/ChangeOrder.Data/Outbox/Payloads/ChangeOrderCancelledPayload.cs`
  - Crear `src/ChangeOrder.Data/Outbox/Payloads/OrderStaleEscalationDuePayload.cs`
- **Dependencias**: T002, T003
- **Descripción**: Un `record` por evento Domain. Campos exactos según design §5.3 (JSON schemas). Cada payload usa `JsonSerializerOptions` con `JsonStringEnumConverter` (decisión D2 — enums como string). `IOutboxPayload` es un marker vacío que documenta intención (no fuerza forma — Data es libre de evolucionar la wire-shape).
- **Acceptance**: `dotnet build` compila sin warnings; revisión confirma que ningún payload incluye el campo `correlationId` (D9 — vive en columna).
- **Tamaño**: `s`

### T015 — Serializer `OutboxEventSerializer` con registry estático

- **Capa Onion**: Data
- **Archivos**:
  - Crear `src/ChangeOrder.Data/Outbox/OutboxEventSerializer.cs`
- **Dependencias**: T014
- **Descripción**: Clase `sealed` que expone:
  - `(string EventType, string PayloadJson) Serialize(IDomainEvent evt)` — mapea Domain event → payload Data → JSON. Usa `Dictionary<Type, Func<IDomainEvent, IOutboxPayload>>` estático.
  - `IDomainEvent Deserialize(string eventType, string payloadJson)` — lookup por nombre FQN (`StringComparison.Ordinal`) → tipo payload → `JsonSerializer.Deserialize` → mapea a Domain event.
  - Ambos diccionarios viven en miembros `static readonly` para evitar reflexión en hot path.
- **Acceptance**: `dotnet build` compila; tests round-trip cubren todos los eventos (T029).
- **Tamaño**: `m`

### T016 — Repositorio `OutboxRepository`

- **Capa Onion**: Data
- **Archivos**:
  - Crear `src/ChangeOrder.Data/Repositories/OutboxRepository.cs`
- **Dependencias**: T007, T009, T015
- **Descripción**: Implementa `IOutboxRepository`. `ClaimPendingAsync` ejecuta SQL raw via `_dbContext.Database.SqlQuery<...>` o `FromSqlInterpolated` con el query exacto de design §2 D3:
  ```sql
  SELECT TOP (@batch) [Id], [EventType], [Payload], [Attempts], [CorrelationId]
  FROM [dbo].[OutboxMessages] WITH (UPDLOCK, READPAST)
  WHERE [ProcessedAtUtc] IS NULL
    AND [DeadLetteredAtUtc] IS NULL
    AND ([NextRetryAtUtc] IS NULL OR [NextRetryAtUtc] <= SYSUTCDATETIME())
  ORDER BY [OccurredAtUtc]
  ```
  Los demás métodos usan UPDATE directos via `ExecuteSqlInterpolatedAsync` o tracking-then-save. `AppendAsync` serializa via `OutboxEventSerializer` y agrega al `DbSet`. Todos los `await` con `.ConfigureAwait(false)`. Cada `catch` (si lo hay) loggea con `ILogger<OutboxRepository>`.
- **Acceptance**: `dotnet build` compila sin warnings; tests de claim concurrente (T030) prueban READPAST.
- **Tamaño**: `m`

### T017 — `ChangeOrderRepository.ListStalePendingApprovalAsync`

- **Capa Onion**: Data
- **Archivos**:
  - Modificar `src/ChangeOrder.Data/Repositories/ChangeOrderRepository.cs`
- **Dependencias**: T008, T011
- **Descripción**: Implementar el método declarado en T008 con query LINQ:
  ```csharp
  return await _dbContext.ChangeOrders
      .AsNoTracking()
      .Where(o => o.Status == OrderStatus.PendingApproval
                  && o.LastStatusChangeAt < thresholdUtc
                  && !o.IsDeleted)
      .OrderBy(o => o.LastStatusChangeAt)
      .Skip((page - 1) * pageSize)
      .Take(pageSize)
      .Select(o => o.Id)
      .ToListAsync(ct)
      .ConfigureAwait(false);
  ```
  El query plan debería usar `IX_ChangeOrders_PendingApproval_Stale` (T011).
- **Acceptance**: `dotnet build` compila; query plan revisado contra SQL Server local con `SET STATISTICS IO ON` muestra index seek; cubierto por test T032.
- **Tamaño**: `s`

### T018 — `EfUnitOfWork`: drain de eventos + captura de `CorrelationId`

- **Capa Onion**: Data
- **Archivos**:
  - Modificar `src/ChangeOrder.Data/Repositories/UnitOfWork.cs` (o nombre actual del UoW concreto, `EfUnitOfWork.cs`)
- **Dependencias**: T009, T015, T018-precondition (existe ya `_dbContext` + `IUnitOfWorkTransaction`)
- **Descripción**: Inyectar `OutboxEventSerializer` y `TimeProvider` por constructor. Crear método privado `DrainDomainEventsToOutbox()` que:
  1. Itera `_dbContext.ChangeTracker.Entries()` filtrando entradas cuyo `Entity` implementa el contrato de aggregate root con `DomainEvents` (chequear por presencia del miembro vía un check de tipo `is IAggregateRoot` si existe, o reflexión sobre `DomainEvents` property — preferentemente introducir un marker `IAggregateRoot` en Domain si no existe, pero esto se difiere si el aggregate único es `ChangeOrder`).
  2. Para cada evento en `DomainEvents`, llama `_serializer.Serialize(evt)`, captura `correlationId = LogContext.GetProperty("CorrelationId")?.ToString()` (D9 — usar `Serilog.Context.LogContext` API o helper interno), construye `OutboxMessageEntity` con `Id = Guid.NewGuid()`, `OccurredAtUtc = evt.OccurredAtUtc`, `CreatedAt = _timeProvider.GetUtcNow().UtcDateTime`, `CorrelationId = correlationId` y lo agrega via `_dbContext.OutboxMessages.Add(...)`.
  3. Llama `aggregate.ClearDomainEvents()` al final.
  4. El drain corre **antes** de `_dbContext.SaveChangesAsync` dentro del MISMO scope transaccional (la transacción explícita del `IUnitOfWorkTransaction` si está abierta, o la implícita del `SaveChangesAsync` si no).
  Aplicar el drain en LAS TRES variantes del UoW: `SaveChangesAsync`, `SaveChangesWithDuplicateDetectionAsync`, `SaveChangesWithConcurrencyDetectionAsync` (design §6 nota).
- **Acceptance**: `dotnet build` compila sin warnings; tests T030 cubren atomicidad drain + commit + rollback; revisión manual confirma que las tres variantes invocan el helper.
- **Tamaño**: `m`

### T019 — DI: registrar `IOutboxRepository` y `OutboxEventSerializer`

- **Capa Onion**: Data
- **Archivos**:
  - Modificar `src/ChangeOrder.Data/Extensions/ServiceCollectionExtensions.cs`
- **Dependencias**: T015, T016
- **Descripción**: En el método `AddDataLayer` (o nombre actual), agregar `services.AddScoped<IOutboxRepository, OutboxRepository>();` y `services.AddSingleton<OutboxEventSerializer>();` (singleton porque su estado son diccionarios `static readonly`). Si `TimeProvider` no está registrado todavía, agregar `services.TryAddSingleton(TimeProvider.System);`.
- **Acceptance**: `dotnet build` compila; integration test que resuelve `IOutboxRepository` desde el container pasa.
- **Tamaño**: `xs`

---

## Fase 3 — Business (handlers + dispatcher + abstracción email)

### T020 — Abstracciones: `IDomainEventHandler<T>`, `IDomainEventDispatcher`, `IEmailSender`

- **Capa Onion**: Business
- **Archivos**:
  - Crear `src/ChangeOrder.Business/Abstractions/IDomainEventHandler.cs`
  - Crear `src/ChangeOrder.Business/Abstractions/IDomainEventDispatcher.cs`
  - Crear `src/ChangeOrder.Business/Abstractions/IEmailSender.cs`
- **Dependencias**: T001
- **Descripción**:
  - `IDomainEventHandler<TEvent>` donde `TEvent : IDomainEvent`: método `Task<Result<TVoid, Error>> HandleAsync(TEvent evt, CancellationToken ct)`. Usar el tipo `Result<TVoid, Error>` ya existente en el proyecto (ADR-0002).
  - `IDomainEventDispatcher`: `Task<Result<TVoid, Error>> DispatchAsync(IDomainEvent evt, CancellationToken ct)`.
  - `IEmailSender`: `Task<Result<TVoid, Error>> SendAsync(EmailMessage message, CancellationToken ct)`. `EmailMessage` es un `record` declarado en el mismo archivo (excepción justificada — es DTO del contrato) con `To, Subject, BodyHtml, BodyText`.
- **Acceptance**: `dotnet build` compila; namespace `ChangeOrder.Business.Abstractions`; ningún `using` de `Microsoft.EntityFrameworkCore.*`.
- **Tamaño**: `s`

### T021 — Implementación: `DomainEventDispatcher`

- **Capa Onion**: Business
- **Archivos**:
  - Crear `src/ChangeOrder.Business/Events/DomainEventDispatcher.cs`
- **Dependencias**: T020
- **Descripción**: Clase `sealed` que implementa `IDomainEventDispatcher`. Recibe `IServiceProvider` por ctor. `DispatchAsync` resuelve `IDomainEventHandler<TConcrete>` via `serviceProvider.GetServices(typeof(IDomainEventHandler<>).MakeGenericType(evt.GetType()))`, invoca cada handler vía reflexión (o `dynamic`), agrega resultados. Si CUALQUIER handler retorna `Result.Failure` retryable, el dispatcher retorna `Result.Failure` retryable (composición — el processor decide retry). Logger inyectado para registrar el evento procesado / error. `.ConfigureAwait(false)` en cada `await`.
- **Acceptance**: `dotnet build` compila; tests T031 cubren resolución y agregación.
- **Tamaño**: `m`

### T022 — Handler: `SendOrderCreatedEmailHandler`

- **Capa Onion**: Business
- **Archivos**:
  - Crear `src/ChangeOrder.Business/EventHandlers/SendOrderCreatedEmailHandler.cs`
- **Dependencias**: T020
- **Descripción**: `IDomainEventHandler<ChangeOrderSubmittedForApproval>` sealed. Construye `EmailMessage` con asunto y cuerpo (ver §5 design — implementa idempotencia via `Message-Id: <{OrderId}.created@changeorder>` que se pasa al `EmailMessage` como header opcional — agregarlo al record `EmailMessage` si no está; ver T020). Invoca `_emailSender.SendAsync(...)` y propaga el `Result`.
- **Acceptance**: `dotnet build` compila; test happy-path + retryable + permanent (T033).
- **Tamaño**: `s`

### T023 — Handlers: `SendApprovalNotificationHandler` (dos clases)

- **Capa Onion**: Business
- **Archivos**:
  - Crear `src/ChangeOrder.Business/EventHandlers/SendApprovalNotificationHandler.cs` (handler de `ApprovalRecorded`)
  - Crear `src/ChangeOrder.Business/EventHandlers/SendFullApprovalNotificationHandler.cs` (handler de `ChangeOrderFullyApproved`)
- **Dependencias**: T020
- **Descripción**: Dos clases sealed separadas (regla "un tipo por archivo"). El handler de `ApprovalRecorded` ramifica internamente por `Verdict` (`Approved` vs `Rejected`) y envía el correo adecuado — no requiere evento dedicado para el rechazo (Q3 cerrada, design §5.2). `Message-Id` derivado de `({OrderId}.{Level}.{Verdict}@changeorder)`. El handler de `ChangeOrderFullyApproved` envía email final al requester con `Message-Id: ({OrderId}.fullyApproved@changeorder)`.
- **Acceptance**: `dotnet build` compila; tests T033 cubren ambas ramas (Approved/Rejected) del primer handler y el happy path del segundo.
- **Tamaño**: `s`

### T024 — Handler: `SendStaleOrderEscalationHandler`

- **Capa Onion**: Business
- **Archivos**:
  - Crear `src/ChangeOrder.Business/EventHandlers/SendStaleOrderEscalationHandler.cs`
- **Dependencias**: T020
- **Descripción**: `IDomainEventHandler<OrderStaleEscalationDue>` sealed. Idempotencia por `(OrderId, ScanWindowStartUtc)` (design D5). `Message-Id` derivado de esos dos campos. Envía email de escalación al department head.
- **Acceptance**: `dotnet build` compila; test happy-path + replay idempotente (T033).
- **Tamaño**: `s`

### T025 — DI: registrar dispatcher y handlers (barrido por reflexión)

- **Capa Onion**: Business
- **Archivos**:
  - Modificar `src/ChangeOrder.Business/Extensions/ServiceCollectionExtensions.cs`
- **Dependencias**: T021, T022, T023, T024
- **Descripción**: Registrar `services.AddSingleton<IDomainEventDispatcher, DomainEventDispatcher>();` (singleton es OK porque depende de `IServiceProvider`). Hacer barrido reflexivo en el ensamblado para registrar todas las implementaciones de `IDomainEventHandler<>` como `Scoped` — replicar exactamente el patrón existente de barrido de `ICommandHandler<,>` si existe en el proyecto. Si no existe, escribir un helper local `RegisterDomainEventHandlers(services)`.
- **Acceptance**: `dotnet build` compila; integration test que resuelve `IDomainEventHandler<ChangeOrderSubmittedForApproval>` desde el container devuelve `SendOrderCreatedEmailHandler`.
- **Tamaño**: `s`

---

## Fase 4 — Host (background services + SMTP + config)

### T026 — Options: `OutboxOptions`, `StaleScannerOptions`, `SmtpOptions`

- **Capa Onion**: Host
- **Archivos**:
  - Crear `src/ChangeOrder.Host/Configuration/OutboxOptions.cs`
  - Crear `src/ChangeOrder.Host/Configuration/StaleScannerOptions.cs`
  - Crear `src/ChangeOrder.Host/Infrastructure/Email/SmtpOptions.cs`
- **Dependencias**: (ninguna)
- **Descripción**: POCOs con propiedades coincidiendo con design §8:
  - `OutboxOptions`: `PollIntervalSeconds (int=2)`, `BatchSize (int=50)`, `Enabled (bool=true)`, nested `Retry` con `BaseBackoffSeconds (int=30)`, `MaxBackoffSeconds (int=1800)`, `MaxAttempts (int=5)`, `JitterPercent (int=15)`.
  - `StaleScannerOptions`: `IntervalMinutes (int=60)`, `ThresholdDays (int=7)`, `PageSize (int=50)`, `Enabled (bool=true)`.
  - `SmtpOptions`: `Host`, `Port (int=587)`, `UseStartTls (bool=true)`, `Username`, `FromAddress`. `Password` se lee de env var, NO se declara aquí (design §8 nota seguridad).
- **Acceptance**: `dotnet build` compila; cada clase tiene su `const string Section = "Outbox"` (o "StaleScanner"/"Smtp") para binding limpio.
- **Tamaño**: `s`

### T027 — `OutboxProcessorService : BackgroundService`

- **Capa Onion**: Host
- **Archivos**:
  - Crear `src/ChangeOrder.Host/BackgroundServices/OutboxProcessorService.cs`
- **Dependencias**: T016, T021, T026
- **Descripción**: Sealed class que extiende `BackgroundService`. Modelo del loop según design §6:
  1. `PeriodicTimer(TimeSpan.FromSeconds(options.PollIntervalSeconds))`.
  2. En cada tick, si `options.Enabled == false` saltar (kill-switch §10).
  3. Abrir scope con `IServiceScopeFactory`, resolver `IOutboxRepository`, `IDomainEventDispatcher`, `OutboxEventSerializer`, `IUnitOfWork`.
  4. `BeginTransactionAsync()` (ADR-0003), `ClaimPendingAsync(batchSize, ct)`.
  5. Por cada fila claimed:
     - `using IDisposable? scope = !string.IsNullOrEmpty(msg.CorrelationId) ? LogContext.PushProperty("CorrelationId", msg.CorrelationId) : null;` (D9).
     - `IDomainEvent evt = serializer.Deserialize(msg.EventType, msg.Payload);`
     - `try { Result r = await dispatcher.DispatchAsync(evt, ct).ConfigureAwait(false); }` con `catch (Exception ex)` que loggea full + trata como retryable.
     - Si `r.IsSuccess`: `MarkProcessedAsync(msg.Id, ct)`.
     - Si `r.IsFailure`: calcular `nextRetryAtUtc` via helper `ComputeBackoff(attempts, options.Retry)` (exponencial con jitter — design D4). Si `attempts + 1 >= MaxAttempts` → `MarkDeadLetterAsync(msg.Id, ct)` + `_logger.LogWarning(...)`; sino → `RecordFailureAsync(msg.Id, error, nextRetryAtUtc, ct)`.
  6. `Commit`.
  Cancelación cooperativa con `stoppingToken`. `.ConfigureAwait(false)` en todo `await`.
- **Acceptance**: `dotnet build` compila sin warnings; tests T034 cubren happy-path, retry y dead-letter; revisión manual confirma uso de `LogContext.PushProperty` y `Enabled` flag.
- **Tamaño**: `l` → **PARTIR** en T027a (loop principal + claim/dispatch/mark) y T027b (cálculo de backoff con jitter + dead-letter + kill-switch + logging detallado).
- **Sub-tareas**:

#### T027a — `OutboxProcessorService` loop core

- **Tamaño**: `m`
- **Descripción**: Esqueleto del `BackgroundService`, `PeriodicTimer`, scope per tick, claim, dispatch, mark processed / fail (sin cálculo de backoff sofisticado — placeholder de retry fijo). Sin kill-switch.
- **Acceptance**: Test T034a happy-path + retry simple pasa.

#### T027b — `OutboxProcessorService` backoff + dead-letter + kill-switch

- **Tamaño**: `s`
- **Descripción**: Sustituir placeholder de retry por `ComputeBackoff(attempts, options.Retry)` con doubling + cap + jitter ±15%. Agregar transición a dead-letter en `attempts + 1 >= MaxAttempts`. Agregar short-circuit cuando `options.Enabled == false`. `_logger.LogWarning(eventId, lastError)` en dead-letter.
- **Acceptance**: Test T034b (dead-letter después de 5 attempts) + T034c (kill-switch) pasan.

### T028 — `StaleOrderScanner : BackgroundService`

- **Capa Onion**: Host
- **Archivos**:
  - Crear `src/ChangeOrder.Host/BackgroundServices/StaleOrderScanner.cs`
- **Dependencias**: T007, T017, T026
- **Descripción**: Sealed class que extiende `BackgroundService`. Loop con `PeriodicTimer(TimeSpan.FromMinutes(options.IntervalMinutes))`. En cada tick:
  1. Si `options.Enabled == false` skip.
  2. Computar `thresholdUtc = TimeProvider.GetUtcNow().UtcDateTime - TimeSpan.FromDays(options.ThresholdDays)`.
  3. Computar `scanWindowStartUtc` redondeado a la hora (D5 — idempotencia clave).
  4. Loop de paginación `page = 1, 2, ...` mientras la página devuelva filas. Por cada página:
     - Abrir scope + `IUnitOfWorkTransaction`.
     - `var ids = await changeOrderRepository.ListStalePendingApprovalAsync(thresholdUtc, options.PageSize, page, ct)`.
     - Por cada `id`: `await outboxRepository.AppendAsync(new OrderStaleEscalationDue(id, /* OrderNumber */, /* LastStatusChangeAt */, scanWindowStartUtc, TimeProvider.GetUtcNow().UtcDateTime), correlationId: $"stale-scan-{scanWindowStartUtc:yyyyMMddHH}", ct)` (decisión operativa: inyectamos correlationId sintético — design §4 columna nota / §3.3 Flow C).
     - Commit. Si falla, log + continuar con la próxima página (idempotencia D5 lo cubre).
  5. Si necesitamos `OrderNumber` o `LastStatusChangeAt` en el payload, el repo actual devuelve solo `Guid` — extender el método (T008/T017) para devolver `record StaleOrderEntry(Guid Id, string OrderNumber, DateTime LastStatusChangeAt)` SI el design lo exige. Ver §5.3 payload `OrderStaleEscalationDue`. **Ambigüedad resuelta**: extender T017 para devolver el record completo en lugar de solo `Guid` — ajuste registrado en T028 como aclaración, no requiere decisión de Jose.
- **Acceptance**: `dotnet build` compila; test T035 (orden fresco ignorado, orden 7d+ stale produce exactamente una fila por ventana) pasa.
- **Tamaño**: `m`

> **Nota a Jose registrada en T028**: para que el payload `OrderStaleEscalationDue` lleve `OrderNumber` y `LastStatusChangeAtUtc` como manda design §5.3, el método de repositorio `ListStalePendingApprovalAsync` (T008/T017) DEBE devolver más que `Guid`. Se ajusta la firma a `IReadOnlyList<StaleOrderEntry>` donde `StaleOrderEntry(Guid Id, string OrderNumber, DateTime LastStatusChangeAt)`. **Resuelto en tasks.md** — no requiere acción del usuario.

### T029 — SMTP adapter: `SmtpEmailSender`

- **Capa Onion**: Host
- **Archivos**:
  - Crear `src/ChangeOrder.Host/Infrastructure/Email/SmtpEmailSender.cs`
- **Dependencias**: T020, T026
- **Descripción**: Sealed class que implementa `IEmailSender`. Adapter delgado sobre `MailKit.SmtpClient` (o `System.Net.Mail.SmtpClient` si se prefiere evitar nuevo package — preferir MailKit por modernidad y soporte de STARTTLS). Lee `SmtpOptions` via `IOptions<SmtpOptions>` y `Password` desde `IConfiguration["Smtp:Password"]` (env var). Cada `Send` envuelto en try/catch que loggea full exception + retorna `Result<TVoid, Error>` con error retryable para fallas transitorias (timeout, conexión) y permanente para 5xx SMTP. Documentar el `Message-Id` opcional del `EmailMessage` (T020) — si está presente, lo setea en el header SMTP.
- **Acceptance**: `dotnet build` compila; test mock con `IEmailSender` no toca SMTP real; smoke test manual contra MailHog (puerto 1025 en dev) opcional.
- **Tamaño**: `m`

### T030 — Wiring DI en Host + `appsettings.json`

- **Capa Onion**: Host
- **Archivos**:
  - Modificar `src/ChangeOrder.Host/Extensions/ServiceCollectionExtensions.cs` (o `Program.cs` si la DI está inline)
  - Modificar `src/ChangeOrder.Host/appsettings.json`
  - Modificar `src/ChangeOrder.Host/appsettings.Development.json`
- **Dependencias**: T026, T027, T028, T029
- **Descripción**:
  1. Registrar options: `services.Configure<OutboxOptions>(configuration.GetSection("Outbox"));` (idem `StaleScannerOptions`, `SmtpOptions`).
  2. Registrar singletons: `services.AddSingleton<IEmailSender, SmtpEmailSender>();`.
  3. Registrar hosted services: `services.AddHostedService<OutboxProcessorService>();` + `services.AddHostedService<StaleOrderScanner>();`.
  4. Asegurar que `AddDataLayer()` y `AddBusinessLayer()` ya se llaman antes (existente).
  5. `appsettings.json`: agregar las tres secciones de design §8 con defaults.
  6. `appsettings.Development.json`: override `Outbox:PollIntervalSeconds=1`, `StaleScanner:IntervalMinutes=5`, `StaleScanner:ThresholdDays=0`, `Smtp:Host=localhost`, `Smtp:Port=1025`, `Smtp:UseStartTls=false`.
- **Acceptance**: `dotnet build` compila; `dotnet run --project src/ChangeOrder.Host` arranca sin excepciones en startup (verificación local manual); `IHost.Services.GetServices<IHostedService>()` incluye ambos hosted services.
- **Tamaño**: `s`

---

## Fase 5 — Tests (cobertura mínima de design §9)

### T031 — Tests Domain: eventos por transición

- **Capa Onion**: Tests
- **Archivos**:
  - Crear `tests/ChangeOrder.Domain.Tests/Entities/ChangeOrderDomainEventsTests.cs`
- **Dependencias**: T006
- **Descripción**: Un test por método de transición:
  - `Ctor_emits_ChangeOrderSubmittedForApproval`.
  - `RecordApproval_with_Approved_intermediate_emits_only_ApprovalRecorded`.
  - `RecordApproval_with_final_Approved_emits_ApprovalRecorded_and_ChangeOrderFullyApproved`.
  - `RecordApproval_with_Rejected_emits_only_ApprovalRecorded_and_does_not_change_status` (Q3 cerrada).
  - `RecordDeliveryDate_emits_MilestoneDatesUpdated_with_kind_Delivery`.
  - `RecordInitialEvaluationDate_emits_MilestoneDatesUpdated_with_kind_InitialEvaluation`.
  - `RecordProductionDeploy_emits_MilestoneDatesUpdated_with_kind_ProductionDeploy_and_advances_status`.
  - `Cancel_emits_ChangeOrderCancelled`.
  - `UpdateContent_does_not_emit_any_event`.
  - `ClearDomainEvents_empties_the_collection`.
- **Acceptance**: `dotnet test --filter "FullyQualifiedName~ChangeOrderDomainEventsTests"` todos verdes.
- **Tamaño**: `m`

### T032 — Tests Domain: `LastStatusChangeAt` (D1)

- **Capa Onion**: Tests
- **Archivos**:
  - Crear `tests/ChangeOrder.Domain.Tests/Entities/ChangeOrderLastStatusChangeAtTests.cs`
- **Dependencias**: T005, T006
- **Descripción**: Tests:
  - `Ctor_sets_LastStatusChangeAt_to_CreatedAt`.
  - `RecordApproval_advancing_chain_updates_LastStatusChangeAt`.
  - `RecordApproval_with_Rejected_does_NOT_update_LastStatusChangeAt` (porque no mueve Status).
  - `UpdateContent_does_NOT_update_LastStatusChangeAt`.
  - `Cancel_updates_LastStatusChangeAt`.
- **Acceptance**: Todos los tests verdes; uso de `TimeProvider` fake o `DateTime.UtcNow` directo según el patrón del aggregate.
- **Tamaño**: `s`

### T033 — Tests Data: drain atómico en `EfUnitOfWork`

- **Capa Onion**: Tests
- **Archivos**:
  - Crear `tests/ChangeOrder.Data.Tests/Outbox/UnitOfWorkDrainTests.cs`
- **Dependencias**: T013, T018
- **Descripción**: Tests con `Testcontainers.MsSql` (4.11+ ya en solution):
  - `SaveChangesAsync_writes_aggregate_and_outbox_rows_in_single_transaction`.
  - `SaveChangesAsync_rollback_rolls_back_both_aggregate_and_outbox`.
  - `SaveChangesAsync_with_no_domain_events_does_not_insert_outbox_rows`.
  - `SaveChangesAsync_captures_CorrelationId_from_LogContext_into_outbox_row` (D9). Usa `LogContext.PushProperty("CorrelationId", "test-corr-123")` en setup.
  - `SaveChangesAsync_with_no_CorrelationId_in_LogContext_persists_NULL_in_outbox_row`.
  - `SaveChangesWithDuplicateDetectionAsync_also_drains` y `SaveChangesWithConcurrencyDetectionAsync_also_drains`.
- **Acceptance**: Todos los tests verdes contra SQL Server real.
- **Tamaño**: `m`

### T034 — Tests Data: claim concurrente con UPDLOCK+READPAST

- **Capa Onion**: Tests
- **Archivos**:
  - Crear `tests/ChangeOrder.Data.Tests/Outbox/OutboxRepositoryClaimTests.cs`
- **Dependencias**: T016
- **Descripción**: Test integración Testcontainers MsSql:
  - `Two_concurrent_claims_return_disjoint_sets` — pre-puebla N filas pending; dispara dos claims en paralelo via `Task.WhenAll`; verifica que la intersección de IDs es vacía y la unión cubre como mucho `2 * batchSize`. Demuestra READPAST en acción.
  - `Claim_respects_NextRetryAtUtc` — fila con `NextRetryAtUtc = now + 1h` no entra al batch.
  - `Claim_respects_DeadLetteredAtUtc` — fila dead-lettered no entra al batch.
  - `Claim_orders_by_OccurredAtUtc` — verifica orden FIFO.
- **Acceptance**: Todos verdes.
- **Tamaño**: `m`

### T035 — Tests Data: round-trip serializer

- **Capa Onion**: Tests
- **Archivos**:
  - Crear `tests/ChangeOrder.Data.Tests/Outbox/OutboxSerializerRoundTripTests.cs`
- **Dependencias**: T015
- **Descripción**: Para cada uno de los 6 eventos Domain (T002 + T003), `[Theory]` que:
  1. Construye una instancia del evento.
  2. `(string evt, string json) = serializer.Serialize(evt)`.
  3. `IDomainEvent back = serializer.Deserialize(evtType, json)`.
  4. Asserta equality estructural campo a campo.
  5. Asserta que `json` NO contiene el string `"correlationId"` (D9 — no debe estar en payload).
- **Acceptance**: Theory passes para todos los 6 eventos.
- **Tamaño**: `s`

### T036 — Tests Business: handlers individuales

- **Capa Onion**: Tests
- **Archivos**:
  - Crear `tests/ChangeOrder.Business.Tests/EventHandlers/SendOrderCreatedEmailHandlerTests.cs`
  - Crear `tests/ChangeOrder.Business.Tests/EventHandlers/SendApprovalNotificationHandlerTests.cs`
  - Crear `tests/ChangeOrder.Business.Tests/EventHandlers/SendFullApprovalNotificationHandlerTests.cs`
  - Crear `tests/ChangeOrder.Business.Tests/EventHandlers/SendStaleOrderEscalationHandlerTests.cs`
- **Dependencias**: T022, T023, T024
- **Descripción**: Por cada handler:
  - Happy path: `IEmailSender` mock (NSubstitute) retorna `Success` → handler retorna `Success`; verifica que `SendAsync` se llamó con `EmailMessage` esperado.
  - Retryable failure: mock retorna `Result.Failure(Error.Retryable)` → handler propaga.
  - Permanent failure: idem permanente.
  - Idempotency replay: invocar `HandleAsync` dos veces con mismo evento → ambas devuelven `Success` y `Message-Id` es idéntico (verificable porque es derivado determinístico de los campos del evento).
  - **Específico para `SendApprovalNotificationHandler`**: dos tests separados — uno con `Verdict = Approved`, otro con `Verdict = Rejected` — verifican que el subject/body cambia y `Message-Id` también.
- **Acceptance**: Todos verdes.
- **Tamaño**: `m`

### T037 — Tests Business: `DomainEventDispatcher`

- **Capa Onion**: Tests
- **Archivos**:
  - Crear `tests/ChangeOrder.Business.Tests/Events/DomainEventDispatcherTests.cs`
- **Dependencias**: T021
- **Descripción**: Tests:
  - `Dispatch_resolves_correct_handler_for_concrete_event_type`.
  - `Dispatch_with_no_handler_registered_returns_Success` (decisión razonable: no handler = nada que hacer; o bien `Failure` según diseño — alinear con el comportamiento implementado en T021 y registrar en este test).
  - `Dispatch_with_multiple_handlers_invokes_all_and_aggregates_results`.
  - `Dispatch_with_one_handler_failing_retryable_returns_retryable_failure_overall`.
  - `Dispatch_with_handler_throwing_exception_logs_and_returns_retryable_failure`.
- **Acceptance**: Todos verdes; build harness usa `ServiceCollection` real con fakes.
- **Tamaño**: `s`

### T038 — Tests Host: `OutboxProcessorService` retry + dead-letter

- **Capa Onion**: Tests (carpeta `Presentation.Tests` por convención existente — design §7.6)
- **Archivos**:
  - Crear `tests/ChangeOrder.Presentation.Tests/HostedServices/OutboxProcessorRetryTests.cs`
- **Dependencias**: T027a, T027b
- **Descripción**: Integration test usando `WebApplicationFactory<Program>` + Testcontainers MsSql + `appsettings.Test.json` con `PollIntervalSeconds=1`, `MaxAttempts=3`:
  - Sembrar una fila outbox cuyo handler está configurado para retornar `Failure.Retryable` siempre (handler fake registrado vía DI override).
  - Esperar (con timeout) que `Attempts` crezca a `3`.
  - Verificar que `NextRetryAtUtc` se actualiza con backoff exponencial entre intentos.
  - Verificar que después del intento `3` falla, la fila gana `DeadLetteredAtUtc != NULL`.
  - Verificar log `Warning` con event id + last error.
  - Test adicional: kill-switch (`Enabled = false`) deja la fila intacta tras N segundos.
- **Acceptance**: Test verde con timeout razonable (15s); usa polling con `await Task.Delay` + check.
- **Tamaño**: `m`

### T039 — Tests Host: `StaleOrderScanner`

- **Capa Onion**: Tests
- **Archivos**:
  - Crear `tests/ChangeOrder.Presentation.Tests/HostedServices/StaleOrderScannerTests.cs`
- **Dependencias**: T028
- **Descripción**: Integration test con Testcontainers MsSql + `ThresholdDays=0`, `IntervalMinutes=` (interval muy corto para test, ej. usar trigger manual):
  - Pre-poblar dos `ChangeOrder` en `PendingApproval`: uno con `LastStatusChangeAt = now - 8d` (stale), otro con `LastStatusChangeAt = now` (fresh).
  - Disparar manualmente un tick del scanner (extraer la lógica del tick a un método `internal` para tests).
  - Verificar que `OutboxMessages` contiene **exactamente una** fila con `EventType = "OrderStaleEscalationDue"` para el order stale, y ninguna para el fresh.
  - Disparar un segundo tick dentro de la misma ventana horaria → todavía una fila (idempotencia por `ScanWindowStartUtc` D5).
  - Verificar que `CorrelationId` de la fila tiene el formato `stale-scan-yyyymmddhh`.
- **Acceptance**: Test verde.
- **Tamaño**: `m`

### T040 — Test concurrencia: 99 órdenes simultáneas con drain activo

- **Capa Onion**: Tests
- **Archivos**:
  - Modificar `tests/ChangeOrder.Data.Tests/OrderNumberConcurrencyTests.cs` (existente)
- **Dependencias**: T018
- **Descripción**: El test `NinetyNineConcurrentCreates_Produce99DistinctOrderNumbers` debe seguir verde con el drain habilitado en `EfUnitOfWork`. Agregar assertion adicional: tras la corrida, `OutboxMessages.Count(o => o.EventType == "ChangeOrder.Domain.Events.ChangeOrderSubmittedForApproval") == 99`.
- **Acceptance**: Test pasa en local con Testcontainers MsSql; verifica el invariante de proposal §"Success Criteria" + cobertura de drain bajo concurrencia.
- **Tamaño**: `s`

---

## Fase 6 — Docs, ADR y cierre

### T041 — Actualizar `README.md` con sección EDA

- **Capa Onion**: Docs
- **Archivos**:
  - Modificar `README.md` (raíz del repo)
- **Dependencias**: (al final, después de T040)
- **Descripción**: Agregar sección "Domain Events + Outbox" con: lista de eventos emitidos, ubicación de la tabla `OutboxMessages`, cómo configurar `Outbox` / `StaleScanner` / `Smtp` secciones, kill-switch `Enabled = false`. Mantener tono conciso (1–2 párrafos + tabla).
- **Acceptance**: `README.md` builds en cualquier markdown renderer; revisión humana confirma coherencia con código mergeado.
- **Tamaño**: `xs`

### T042 — Actualizar `CLAUDE.md` con componentes nuevos

- **Capa Onion**: Docs
- **Archivos**:
  - Modificar `CLAUDE.md` (raíz del repo)
- **Dependencias**: (al final)
- **Descripción**: Agregar entrada en "Piezas arquitectónicas clave" describiendo `OutboxProcessorService`, `StaleOrderScanner`, `IDomainEvent`, `LastStatusChangeAt`, y la decisión D9 (`CorrelationId` en columna). Referenciar ADR-0009 + design.md.
- **Acceptance**: Revisión humana confirma que no hay referencias a tipos inexistentes.
- **Tamaño**: `xs`

### T043 — Promover ADR-0009 a `Accepted`

- **Capa Onion**: Docs
- **Archivos**:
  - Modificar `Docs/adr/0009-eda-domain-events-outbox.md` (cambiar SOLO el campo `Status: Proposed` → `Status: Accepted` + agregar `Date-Accepted: 2026-MM-DD`)
- **Dependencias**: T041, T042 (y todos los anteriores merged)
- **Descripción**: Esta tarea es el último paso del cambio, después de que la implementación esté verificada (verify pass) y mergeada a `main`. NO se ejecuta durante `apply` — se ejecuta como parte del archive del SDD. Marcada aquí para que el orquestador sepa que existe.
- **Acceptance**: ADR-0009 muestra `Status: Accepted`; el cuerpo del ADR queda intacto (decisiones inmutables).
- **Tamaño**: `xs`

### T044 — Archive SDD `002-eda-outbox-foundation`

- **Capa Onion**: Docs / SDD lifecycle
- **Archivos**:
  - Mover (o copiar + marcar) `specs/002-eda-outbox-foundation/` a la sección de archived según convención del proyecto (a confirmar con `/sdd-archive`).
- **Dependencias**: T043
- **Descripción**: Ejecutar `/sdd-archive 002-eda-outbox-foundation` para sincronizar specs deltas a specs main (si aplica al proyecto) y cerrar el ciclo.
- **Acceptance**: `mem_search "sdd/eda-outbox-foundation"` devuelve la entrada del archive; carpeta movida o marcada como `Status: Archived` en el header.
- **Tamaño**: `xs`

---

## Resumen agregado

### Tareas por fase

| Fase | Cantidad | Tareas |
|------|----------|--------|
| Domain | 8 | T001–T008 |
| Data | 11 | T009–T019 |
| Business | 6 | T020–T025 |
| Host | 6 | T026, T027a, T027b, T028, T029, T030 |
| Tests | 10 | T031–T040 |
| Docs y cierre | 4 | T041–T044 |
| **Total** | **45** | (44 IDs + T027 partida en T027a/T027b) |

### Distribución de esfuerzo

| Tamaño | Cantidad |
|--------|----------|
| `xs` | 13 |
| `s` | 16 |
| `m` | 14 |
| `l` | 0 (partidas) |
| **Total** | **43 unidades de granularidad** |

> Conversión aproximada (rango medio): `xs`=20min, `s`=75min, `m`=180min, `l`=300min. Estimación agregada: `13*20 + 16*75 + 14*180 + 0*300 = 260 + 1200 + 2520 = 3980 minutos ≈ 66 horas` de trabajo enfocado de un único developer, sin contar revisiones de PR ni iteraciones de feedback.

### Ruta crítica (cadena de dependencias más larga)

```
T001 (marker)
  → T002 (records transición)
    → T006 (aggregate emite eventos)
      → T011 (mapping LastStatusChangeAt)
        → T013 (migración EF)
          → T018 (drain en UoW)
            → T027a (processor loop)
              → T027b (backoff + dead-letter)
                → T038 (test retry + dead-letter)
                  → T041/T042 (docs)
                    → T043 (ADR → Accepted)
                      → T044 (archive)
```

12 tareas en serie en la ruta crítica. Las fases Business (T020–T025), tests de Domain (T031–T032), serializer round-trip (T035) y `StaleOrderScanner` (T028) pueden paralelizarse parcialmente con la cola Data/Host.

**Primera tarea ejecutable**: `T001` (sin dependencias).

### Resoluciones aplicadas en este `tasks.md` (no requieren acción de Jose)

1. **T008/T017 firma de repo stale**: el método retornaba `Guid` en el design §7.2 pero el payload `OrderStaleEscalationDue` (§5.3) requiere `OrderNumber` y `LastStatusChangeAt`. Se ajusta a `IReadOnlyList<StaleOrderEntry(Guid Id, string OrderNumber, DateTime LastStatusChangeAt)>` — registrado en T008/T017/T028.
2. **T028 `CorrelationId` para eventos sintéticos**: el design §4 dejó dos opciones (NULL o `stale-scan-<yyyymmddhh>`). Se elige la opción **(b) sintético determinístico** para preservar trazabilidad operativa de la corrida del scanner. Format: `stale-scan-{yyyyMMddHH}` UTC. Registrado en T028.
3. **T027 partición**: la tarea original era `l`; se parte en `T027a` (loop core, `m`) + `T027b` (backoff + dead-letter + kill-switch, `s`) para mantener atomicidad.
4. **T020 `EmailMessage.MessageId` header**: el design implica `Message-Id` headers (D5) pero no lo declara explícitamente en el contrato `EmailMessage`. Se agrega como `string? MessageId` opcional en el record para que cada handler lo setee determinísticamente.

### BANDERAS para Jose

Ninguna. Todas las ambigüedades encontradas al granularizar quedaron resueltas en este documento sin necesidad de decisión humana adicional. El design.md (D1–D9) cubrió todo el espacio de decisiones.

---

## Cross-Reference

- Proposal: [`proposal.md`](./proposal.md)
- Design: [`design.md`](./design.md)
- ADR-0009: [`../../Docs/adr/0009-eda-domain-events-outbox.md`](../../Docs/adr/0009-eda-domain-events-outbox.md)
- Rules del proyecto: [`../../Docs/ChangeOrder.Api.Rules.md`](../../Docs/ChangeOrder.Api.Rules.md)
- Convenciones repo: [`../../CLAUDE.md`](../../CLAUDE.md)
