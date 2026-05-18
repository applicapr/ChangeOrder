# ADR-0009: Domain Events + Outbox Pattern para notificaciones y escalamientos por tiempo

- **Status**: Proposed
- **Fecha**: 2026-05-18
- **Decisores**: Jose Lara
- **Tags**: architecture, eda, messaging, domain, data, background-workers

## Context

Al 2026-05-18, ChangeOrder.Api no tiene ninguna pieza de Event-Driven Architecture: cero Domain Events, cero Outbox, cero Sagas, cero brokers. El único `BackgroundService` vivo es `IdempotencyCleanupService`, dedicado a housekeeping de claves de idempotencia. La auditoría arquitectónica de la fecha (`Docs/Auditoria-Arquitectura-2026-05-18.md`) confirma que el grafo Onion + CQRS + Hexagonal cumple al 100%, pero también deja constancia de que no existe infraestructura de eventos.

Tres requisitos nuevos exigen revisar esa decisión:

1. **Notificación por email** cuando se crea una orden o cuando el agregado `ChangeOrder` transiciona de estado (por ejemplo `Draft -> PendingApproval -> Approved`).
2. **Escalamiento automático por inactividad**: una orden que queda 7 días en `PendingApproval` sin cambios debe disparar un proceso de aviso/escalamiento — un caso de SLA basado en timer, no en un comando del usuario.
3. **Cliente WPF futuro** que consumirá la API y necesitará notificaciones en tiempo real (probablemente vía SignalR), alimentadas por los mismos eventos que dan origen al email.

Hoy, la única forma de cumplir esos requisitos sería ejecutar todo el side-effect (SMTP, futuras llamadas a ERP, push a SignalR) **dentro** del handler de comando, en la misma transacción que persiste el agregado. Eso acopla la transacción del dominio con dependencias externas frágiles: si el envío de email falla, se pierde la aprobación; si el SMTP está lento, la API responde lento; si mañana hay un consumidor adicional, hay que tocar el handler.

Además, ADR-0002 (Result Pattern retryable para deadlock SQL 1205) y ADR-0003 (`IUnitOfWorkTransaction`) ya establecieron que la transacción del agregado debe ser corta, determinística y aislada de detalles de infraestructura. Mezclar side-effects externos en esa transacción contradice esa línea.

## Decision

Adoptamos **Domain Events in-process + Outbox Pattern transaccional + scheduled scanner para inactividad** como infraestructura interna de eventos del proyecto. La decisión se compone de seis piezas concretas:

1. **`IDomainEvent`** (marker interface) vive en `ChangeOrder.Domain/Abstractions/`. Sin dependencias externas — solo BCL, coherente con la regla de ADR-0001 (Domain no referencia nada).
2. **Aggregate root con eventos**: el agregado `ChangeOrder` mantiene una lista interna `_domainEvents` y expone `IReadOnlyCollection<IDomainEvent> DomainEvents { get; }` + `ClearDomainEvents()`. Los eventos se acumulan al mutar el agregado y se drenan al persistir.
3. **Tabla `OutboxMessages`** en SQL Server, con columnas: `Id` (GUID), `Type` (string, nombre completo del evento), `Payload` (JSON serializado), `OccurredAt` (UTC), `ProcessedAt` (UTC nullable), `RetryCount` (int), `NextRetryAt` (UTC nullable), `LastError` (string nullable).
4. **`UnitOfWork.SaveChangesAsync`** lee `DomainEvents` de cada aggregate root tracked en el `DbContext`, los serializa, los inserta como filas en `OutboxMessages` **dentro de la misma transacción** que persiste los cambios del agregado, y luego invoca `ClearDomainEvents()`. Esto da garantía at-least-once: o se persiste el agregado **y** sus eventos, o no se persiste nada.
5. **`OutboxProcessorService : BackgroundService`** en `Host` que polea `OutboxMessages` cada N segundos (configurable), resuelve handlers in-process por `Type`, los invoca, marca `ProcessedAt` en éxito, o incrementa `RetryCount` y agenda `NextRetryAt` en falla. Los handlers viven en `Business` (suscriptores) y en `Host` (adaptadores: SMTP, SignalR futuro).
6. **`StaleOrderScanner : BackgroundService`** que cada hora consulta órdenes en `PendingApproval` con `LastStatusChangeAt < UtcNow - 7d`, y por cada una emite un evento `OrderStaleEscalationDue` que va por el mismo Outbox. El escaneo es idempotente: el handler de `OrderStaleEscalationDue` marca la orden como ya escalada para no reemitir.

Las dos primeras piezas son cambios en Domain. Las piezas 3 y 4 son cambios en Data. Las piezas 5 y 6 son nuevas en Host. Presentation no se toca.

## Alternatives considered

- **A — No-EDA, ejecutar side-effects síncronos dentro del handler de comando**. Descartada porque acopla la transacción del agregado con dependencias externas (SMTP hoy, ERP/SignalR mañana). Una falla de SMTP haría perder la aprobación; un SMTP lento degradaría el p95 de la API. Además, romperia ADR-0002/0003 al introducir trabajo no-determinístico dentro de la transacción retryable.
- **C — Broker completo (MassTransit + RabbitMQ o Azure Service Bus)**. Descartada por over-engineering en el scope actual. No hay todavía un consumidor fuera del proceso ChangeOrder.Api; los handlers iniciales (email, SignalR del WPF futuro) viven dentro del mismo host. El día que aparezca un consumidor real cross-service, esta decisión se revisita con un ADR nuevo que **supersede** este (la abstracción `IDomainEvent` + Outbox migra naturalmente a un publisher externo).
- **D — Saga framework completo (MassTransit Saga, NServiceBus)**. Descartada. No hay procesos distribuidos multi-paso con compensaciones. El caso de 7 días no es una saga: es un scan periódico contra una columna `LastStatusChangeAt`. Saga framework sería complejidad pagada sin caso de uso real.

## Consequences

### Positivas

- **Side-effects desacoplados** del path principal — la transacción del agregado vuelve a ser corta y determinística. SMTP, SignalR o ERP pueden fallar o estar lentos sin afectar la respuesta de la API.
- **Reintentos automáticos** vía `RetryCount` + `NextRetryAt` sin contaminar la lógica del handler de comando.
- **Un solo origen de eventos sirve a múltiples consumidores**: email hoy, SignalR/WPF mañana, ERP/auditoría pasado. Añadir un consumidor es registrar un handler nuevo, no tocar el agregado.
- **ADR-0002 sigue válido**: el retry del deadlock 1205 sigue ocurriendo en la transacción que escribe agregado + outbox; los eventos solo se procesan después de que esa transacción haya commiteado.
- **Escalamiento de 7 días resuelto sin Saga** — `StaleOrderScanner` es un worker simple, sin máquina de estados externa.

### Negativas

- **Complejidad operacional** — tabla nueva, dos workers nuevos (`OutboxProcessor`, `StaleOrderScanner`), monitoreo de backlog del Outbox y de `RetryCount` alto.
- **Orden de procesamiento no garantizado** — el Outbox procesa por `OccurredAt` pero con concurrencia limitada; los handlers **deben ser idempotentes** (por ejemplo, registrar `ProcessedAt` en el mensaje y deduplicar por `Id` de evento si llega a haber consumidores externos en el futuro).
- **Disciplina de versionado** — si los eventos llegan a salir del proceso (broker externo en un ADR futuro), el `Payload` JSON deja de ser un detalle interno y pasa a ser contrato. Hay que versionar `Type` con cuidado desde el día uno.
- **Latencia de side-effect** — el email no sale dentro del request del usuario; sale segundos después cuando el worker procesa el Outbox. Para el caso de uso (notificación) es aceptable.

### Neutras

- ADRs 0001–0008 quedan vigentes sin modificación. Domain/Business/Presentation no cambian estructuralmente — solo el aggregate root y `UnitOfWork.SaveChangesAsync` reciben extensiones puntuales.
- Migración EF Core nueva para crear la tabla `OutboxMessages` con sus índices (`ProcessedAt IS NULL`, `NextRetryAt`).
- Configuración nueva en `appsettings.json` para intervalo de polling, tamaño de batch y backoff de retries.

## Compliance / Validación

- **Test de unidad** sobre el agregado `ChangeOrder` que verifica que las transiciones de estado producen los eventos esperados en `DomainEvents`.
- **Test de integración** que crea una orden, observa que el `OutboxMessages` tiene una fila con `ProcessedAt = NULL`, ejecuta una vuelta del `OutboxProcessorService` y verifica que la fila quede con `ProcessedAt != NULL`.
- **Test de integración** que simula un handler que arroja: la fila debe quedar con `RetryCount > 0`, `NextRetryAt` futuro y `LastError` poblado.
- **Test de integración** del `StaleOrderScanner`: una orden con `LastStatusChangeAt < UtcNow - 7d` produce un `OrderStaleEscalationDue` en el Outbox; una orden más fresca no.
- **Code review**: cualquier handler de comando que invoque SMTP, SignalR o cualquier side-effect externo directamente (sin pasar por evento + Outbox) debe ser rechazado en PR a partir de la aceptación de este ADR.
- **Monitoreo**: backlog de `OutboxMessages WHERE ProcessedAt IS NULL` y `MAX(RetryCount)` quedan como métricas operativas.

## Referencias

- `Docs/Auditoria-Arquitectura-2026-05-18.md` — auditoría que confirma la inexistencia previa de EDA.
- `specs/002-eda-outbox-foundation/` — proposal SDD asociado que detallará la implementación (proposal, specs, design, tasks).
- [ADR-0001](0001-onion-architecture-cqrs.md) — estructura Onion + CQRS que este ADR extiende sin romper.
- [ADR-0002](0002-result-pattern-retryable-deadlock.md) — Result retryable para deadlock 1205, vigente sin cambios.
- [ADR-0003](0003-unit-of-work-transaction-abstraction.md) — `IUnitOfWorkTransaction`, base sobre la que el Outbox commitea atómicamente.
