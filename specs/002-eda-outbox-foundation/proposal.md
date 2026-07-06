# Proposal: EDA Outbox Foundation

**Change**: `eda-outbox-foundation`
**Folder**: `specs/002-eda-outbox-foundation/`
**Date**: 2026-05-18
**Status**: Draft (proposal phase)
**Related ADR**: [`Docs/adr/0009-eda-domain-events-outbox.md`](../../Docs/adr/0009-eda-domain-events-outbox.md) (in flight)

## Intent

Introduce the minimum Event-Driven Architecture infrastructure (Domain Events + Transactional Outbox + scheduled scanner) that lets `ChangeOrder.Api` react to `ChangeOrder` aggregate state transitions with side effects decoupled from the primary database transaction.

Today, every cross-cutting reaction to a change order transition (sending an email, escalating a stale approval, notifying a future WPF client) would have to be wired inline in the command handler, either inside the same transaction (slow, fragile, ties business code to infrastructure) or fire-and-forget after commit (non-atomic, lost on crash). Neither option is acceptable under the current Onion + Result-Pattern + Auditability constraints.

The EDA foundation closes this gap with three pieces that fit the existing onion without violating any of its dependency rules:

1. **Domain Events** — pure records emitted by the aggregate inside `ChangeOrder.Domain`, no external dependencies.
2. **Transactional Outbox** — a `OutboxMessages` table in `ChangeOrder.Data`, written in the same EF Core transaction as the aggregate change, guaranteeing at-least-once delivery without 2PC.
3. **Background dispatcher + scanner** — two `BackgroundService`s in `ChangeOrder.Host` that drain the outbox and detect stale orders, respectively.

This proposal explicitly stays **in-process**. Brokers, sagas, SignalR push and versioned integration events are deferred.

## Scope

### In Scope

1. **`IDomainEvent` marker** in `src/ChangeOrder.Domain/Abstractions/IDomainEvent.cs`. No external dependencies; immutable `record` contract.
2. **Concrete event records** in `src/ChangeOrder.Domain/Events/`:
   - `ChangeOrderSubmittedForApproval`
   - `ApprovalRecorded`
   - `ChangeOrderFullyApproved`
   - `ChangeOrderRejected`
   - `MilestoneDatesUpdated`
   - `OrderStaleEscalationDue` (emitted by the scanner, not the aggregate)
3. **`ChangeOrder` aggregate refactor** in `src/ChangeOrder.Domain/Entities/ChangeOrder.cs` (or equivalent): each transition method (`SubmitForApproval`, `RecordApproval`, `RecordMilestoneDates`, `UpdateContent`, and any other state-changing method) appends a domain event to a private `_domainEvents` collection. Reading and clearing the collection is exposed through a controlled accessor for `Data` to drain.
4. **`OutboxMessages` table** in `ChangeOrder.Data` with EF Core migration. Columns (final shape to be settled in design phase, target schema below):
   - `Id` `uniqueidentifier` PK
   - `OccurredAtUtc` `datetime2`
   - `EventType` `nvarchar(256)` (CLR type name)
   - `Payload` `nvarchar(max)` (JSON, System.Text.Json)
   - `ProcessedAtUtc` `datetime2 NULL`
   - `Attempts` `int NOT NULL DEFAULT 0`
   - `LastError` `nvarchar(max) NULL`
   - `DeadLetteredAtUtc` `datetime2 NULL`
5. **`UnitOfWork.SaveChangesAsync` extension** in `src/ChangeOrder.Data/Persistence/`: before calling the underlying `DbContext.SaveChangesAsync`, drain `IDomainEvent`s from tracked aggregates, serialize each into an `OutboxMessage` row, and persist within the same transaction. Same scope as the aggregate write — atomic by construction.
6. **`OutboxProcessorService : BackgroundService`** in `src/ChangeOrder.Host/HostedServices/`:
   - Polls `OutboxMessages WHERE ProcessedAtUtc IS NULL AND DeadLetteredAtUtc IS NULL` at a configurable cadence (default 2s).
   - Deserializes payload, resolves the handler via DI, invokes it with `Result<Unit, Error>` semantics.
   - Marks `ProcessedAtUtc` on success; increments `Attempts` + records `LastError` on transient failure; sets `DeadLetteredAtUtc` after N attempts (default 5).
7. **`StaleOrderScanner : BackgroundService`** in `src/ChangeOrder.Host/HostedServices/`:
   - Runs hourly.
   - Queries `ChangeOrders` in status `PendingApproval` with `LastUpdatedAt < UtcNow - 7 days`.
   - Emits one `OrderStaleEscalationDue` event per match into the outbox.
8. **`IEmailSender` abstraction** in `src/ChangeOrder.Business/Abstractions/` + an SMTP adapter wired in `Host`. The SMTP implementation is a thin stub now; the interface is the stable seam.
9. **Initial handlers** in `src/ChangeOrder.Business/EventHandlers/`:
   - `SendOrderCreatedEmailHandler` ← `ChangeOrderSubmittedForApproval`
   - `SendApprovalNotificationHandler` ← `ApprovalRecorded`, `ChangeOrderFullyApproved`, `ChangeOrderRejected`
   - `SendStaleOrderEscalationHandler` ← `OrderStaleEscalationDue`
10. **DI registration** through `Extensions/ServiceCollectionExtensions.cs` in each affected layer; composition wired in `Host` only.
11. **Tests**:
    - Update `OrderNumberConcurrencyTests.NinetyNineConcurrentCreates_Produce99DistinctOrderNumbers` to assert that adding the outbox write does not break the 99-distinct invariant under the same concurrency conditions.
    - New `Data.Tests` covering: domain events drained on `SaveChangesAsync`; rollback on aggregate failure also rolls back outbox rows; deserialization round-trip per event type.
    - New `Business.Tests` for each handler (happy path + idempotency on replay).
    - New `Host.Tests` (or in `Presentation.Tests` per existing convention) for `OutboxProcessorService` retry → dead-letter promotion.

### Out of Scope

- **External brokers** (RabbitMQ, Azure Service Bus, Kafka). Deferred until a real cross-process consumer exists.
- **Saga / process manager framework** (MassTransit Saga, NServiceBus). Not needed while every reaction is in-process.
- **SignalR / WebSocket push to a WPF client**. The WPF client does not yet exist; when it lands, a new handler will be added — the proposal leaves the extension point but does not implement it.
- **Versioned Integration Events / contract repository**. Domain events only, in-process, no contract negotiation.
- **MediatR (or any other in-process bus library)**. Handlers will be resolved through the existing DI container with a small dispatcher; introducing MediatR is a separate decision.
- **Replay tooling, outbox UI, archival policy**. Operability is satisfied by logging + dead-letter column; tooling is future work.

## Approach

### Layer placement (Onion-preserving)

| Concern | Project | Justification |
|---|---|---|
| `IDomainEvent`, event records, `_domainEvents` collection on aggregate | `ChangeOrder.Domain` | Pure data + behaviour, zero infra. |
| `OutboxMessage` entity, EF Core config, repository, migration | `ChangeOrder.Data` | Persistence concern — table, columns, indexes. |
| `UnitOfWork` drain logic | `ChangeOrder.Data` | Same place the existing `SaveChangesAsync` interceptor lives. |
| `IEmailSender` abstraction | `ChangeOrder.Business` | Used by handlers; Business already owns service abstractions. |
| Handlers (`*Handler` classes) | `ChangeOrder.Business` | Side-effect orchestration — same layer as command handlers. |
| `OutboxProcessorService`, `StaleOrderScanner` | `ChangeOrder.Host` | `BackgroundService` is hosting; Composition Root wires DI. |
| SMTP adapter (concrete `IEmailSender`) | `ChangeOrder.Host` (or a future `Infrastructure.Email`) | Concrete I/O lives at the edge. |

Dependency graph after the change is identical to today: `Domain ← Business ← Presentation ← Host`, with `Data` parallel to `Business` and only referenced by `Host`. **No new edges introduced.** The dispatcher in `Host` consumes `Business` handlers — which is already an allowed edge.

### Transactional semantics

- Aggregate mutation and outbox insert share the **same EF Core transaction** managed by the existing `IUnitOfWorkTransaction` (ADR-0003). No 2PC, no eventual-consistency window between write and outbox.
- Dispatcher is **at-least-once**. Handlers MUST be idempotent (e.g., dedup key on email subject + order id, or check-then-send pattern).
- Retries on the dispatcher use a fixed schedule (immediate, then exponential backoff capped at 5 attempts). Beyond that, the message is **dead-lettered** (column set, alert logged) and surfaced via Serilog with the event id correlator.

### Error handling

- Handler returns `Result<Unit, Error>` (consistent with the rest of the codebase — ADR-0002).
- Transient errors (`Error.Kind == Retryable`) → bump `Attempts`, leave `ProcessedAtUtc` NULL, retry next poll.
- Permanent errors (`Error.Kind == Permanent`) → dead-letter immediately, no retry.
- Deadlock victim (SQL 1205) on the dispatcher loop is treated as transient; retried under the existing retryable-result convention.

### Polling cadence

- `OutboxProcessorService`: default 2s. Configurable via `appsettings.json` (`Outbox:PollIntervalSeconds`).
- `StaleOrderScanner`: hourly. Configurable (`StaleScanner:IntervalMinutes`).
- Both services support graceful shutdown via `IHostApplicationLifetime` / cancellation token, in line with the existing hosting conventions.

## Affected Areas

| Area | Impact | Description |
|---|---|---|
| `src/ChangeOrder.Domain/Abstractions/IDomainEvent.cs` | New | Marker interface. |
| `src/ChangeOrder.Domain/Events/*.cs` | New | 6 event records. |
| `src/ChangeOrder.Domain/Entities/ChangeOrder.cs` | Modified | Emit events on every state transition; expose drain accessor. |
| `src/ChangeOrder.Data/Entities/OutboxMessage.cs` | New | EF entity. |
| `src/ChangeOrder.Data/Configurations/OutboxMessageConfiguration.cs` | New | EF mapping + indexes (`(ProcessedAtUtc, DeadLetteredAtUtc, OccurredAtUtc)` filtered index for the dispatcher poll). |
| `src/ChangeOrder.Data/Persistence/AppDbContext.cs` | Modified | `DbSet<OutboxMessage>`. |
| `src/ChangeOrder.Data/Persistence/UnitOfWork.cs` (or interceptor) | Modified | Drain domain events, write to outbox in same transaction. |
| `src/ChangeOrder.Data/Migrations/<timestamp>_AddOutboxMessages.cs` | New | EF Core migration. |
| `src/ChangeOrder.Business/Abstractions/IEmailSender.cs` | New | Abstraction. |
| `src/ChangeOrder.Business/EventHandlers/*.cs` | New | Three handlers. |
| `src/ChangeOrder.Business/Extensions/ServiceCollectionExtensions.cs` | Modified | Register handlers + `IEmailSender` consumer. |
| `src/ChangeOrder.Host/HostedServices/OutboxProcessorService.cs` | New | `BackgroundService`. |
| `src/ChangeOrder.Host/HostedServices/StaleOrderScanner.cs` | New | `BackgroundService`. |
| `src/ChangeOrder.Host/Infrastructure/SmtpEmailSender.cs` | New | SMTP adapter (thin). |
| `src/ChangeOrder.Host/Extensions/ServiceCollectionExtensions.cs` | Modified | Wire hosted services and SMTP adapter. |
| `src/ChangeOrder.Host/appsettings.json` | Modified | Outbox + scanner + SMTP config sections. |
| `tests/ChangeOrder.Data.Tests/` | New + Modified | Outbox drain tests; update concurrency test. |
| `tests/ChangeOrder.Business.Tests/` | New | Handler tests. |
| `tests/ChangeOrder.Domain.Tests/` | New | Aggregate event emission tests per transition. |

## Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Domain events leak EF Core or other infra concerns into `Domain` | Medium | High | Code review + Domain layer has zero PackageReferences except `System.*`. Enforced by existing Onion gates. |
| Handler ordering matters but is not guaranteed | Medium | Medium | Document explicitly in the handler skeleton + design.md. Each handler MUST tolerate reordering across event types. |
| Outbox poll latency creates UX gap (e.g., email arrives 2s late) | High | Low | Acceptable for emails; revisit when a low-latency consumer (SignalR push) appears. Polling cadence is config-driven. |
| Email handler fails in bulk (SMTP outage) → dispatcher loop spins | Medium | Medium | Dead-letter after N attempts; alert via Serilog; circuit-breaker is a future ADR if recurrence justifies it. |
| Outbox table grows unbounded | Medium | Medium | Retention/archival is out of scope here but acknowledged. A cleanup job (similar to `IdempotencyCleanupService`) lands in a follow-up change. |
| Concurrency test regresses (the 99-distinct invariant breaks with the new write path) | Low | High | Test is updated as part of this change and is a release gate. |
| Dispatcher picks up the same row twice on parallel instances | Low | High | Single instance for now (operationally enforced). When scale-out lands, switch to `SELECT … FOR UPDATE SKIP LOCKED` equivalent (`UPDLOCK, READPAST` on SQL Server) — captured as a follow-up. |
| Increased `SaveChangesAsync` cost from extra inserts | Medium | Low | Outbox inserts are append-only and indexed for the dispatcher; expected overhead is < 5% on the create path. Measured in design phase. |

## Rollback Plan

The change is additive in the database and additive in DI. Rollback is straightforward:

1. **Code-level rollback** (revert the merge commit): all new types disappear, existing transitions return to no-op emission. The `OutboxMessages` table remains in the database but is orphaned (no writers, no readers).
2. **Database rollback**: run `dotnet ef migrations remove` against the migration name introduced by this change, then `dotnet ef database update <previous>` to drop the table. Safe at any time since no foreign key from `ChangeOrders` points to `OutboxMessages`.
3. **Operational kill-switch (no redeploy)**: set `Outbox:PollIntervalSeconds = 0` (or a new `Outbox:Enabled = false` flag — to be confirmed in design phase) to stop the dispatcher without removing the table. Messages keep accumulating; reaping happens once re-enabled.

No data migration is required for rollback — outbox rows are an append-only log of events, not part of the domain truth.

## Dependencies

- ADR-0009 — Event-Driven Architecture: Domain Events + Outbox (in flight, parallel work). This proposal does not merge until ADR-0009 is `Accepted`.
- ADR-0002 — Result Pattern (retryable deadlock) — reused unchanged for handler return type.
- ADR-0003 — `IUnitOfWorkTransaction` abstraction — reused unchanged; outbox writes participate in the same transaction.
- `Docs/Auditoria-Arquitectura-2026-05-18.md` — confirms the current Onion can absorb the new pieces without restructuring.

No new NuGet packages are required for the in-process baseline. `System.Text.Json` is already transitively available. If `IEmailSender` SMTP adapter uses `MailKit`, that is the only candidate new dependency and lives in `Host`.

## Success Criteria

- [ ] `Domain` still has zero infrastructure PackageReferences after the change (verified by inspecting `ChangeOrder.Domain.csproj`).
- [ ] Every `ChangeOrder` state-transition method emits exactly one domain event, asserted by a dedicated test class per transition.
- [ ] `SaveChangesAsync` writes the aggregate and the outbox rows in a single transaction; rollback of the aggregate rolls back the outbox rows (integration test).
- [ ] `OutboxProcessorService` processes a queued event end-to-end within < 5s of the source `SaveChangesAsync` (default poll cadence).
- [ ] `OutboxProcessorService` dead-letters a permanently-failing message after the configured attempt cap, with the failure recorded in `LastError`.
- [ ] `StaleOrderScanner` emits exactly one `OrderStaleEscalationDue` per stale order per scan window (idempotent over multiple runs — to be enforced by a dedup column in the design phase).
- [ ] `OrderNumberConcurrencyTests.NinetyNineConcurrentCreates_Produce99DistinctOrderNumbers` still passes with the outbox writes enabled.
- [ ] `dotnet build` and `dotnet test` are green with `TreatWarningsAsErrors=true`.
- [ ] `dotnet format` produces no changes after the implementation.
- [ ] ADR-0009 is marked `Accepted` and cross-references this change folder.

## Deferred (next SDD phases)

The following are explicitly NOT part of this proposal and will be produced in subsequent phases of this same change folder:

- `spec.md` — Given/When/Then scenarios per domain event and dispatcher behaviour.
- `design.md` — Sequence diagrams (aggregate write → outbox → dispatcher → handler), retry/dead-letter flow, scanner cadence diagram.
- `tasks.md` — Implementable task breakdown sized for `/sdd-apply`.
- Implementation itself — expected 2026-05-19 or later, gated on ADR-0009 acceptance and review of this proposal.

## Cross-References

- ADR-0009: `Docs/adr/0009-eda-domain-events-outbox.md` (architectural decision)
- ADR-0002: `Docs/adr/0002-result-pattern-retryable-deadlock.md` (retryable error model — reused)
- ADR-0003: `Docs/adr/0003-unit-of-work-transaction-abstraction.md` (transaction abstraction — reused)
- ADR-0001: `Docs/adr/0001-onion-architecture-cqrs.md` (layer rules this proposal must honour)
- Architecture audit: `Docs/Auditoria-Arquitectura-2026-05-18.md`
- Prior feature: `specs/001-change-order-management/` (canonical SDD format reference)
- Project conventions: `Docs/ChangeOrder.Api.Rules.md`, `CLAUDE.md`
