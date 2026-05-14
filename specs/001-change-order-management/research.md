# Phase 0 — Research: Change Order Management

**Feature**: `001-change-order-management`
**Date**: 2026-05-12

This document records the technical decisions that resolve the open questions surfaced during planning. Each entry follows the schema `Decision · Rationale · Alternatives considered`. The two `[NEEDS CLARIFICATION]` markers from the spec were already closed in `/speckit-clarify` (see `spec.md` section `## Clarifications`); they appear here only for completeness alongside the supporting research.

---

## R-1 — Thread-safe `OrderNumber` generation

**Decision**: Inside a single `SaveChangesAsync` transaction, the repository runs:

```sql
SELECT ISNULL(MAX(CAST(RIGHT(OrderNumber, 2) AS INT)), 0) + 1
FROM   dbo.ChangeOrders WITH (UPDLOCK, HOLDLOCK)
WHERE  OrderNumber LIKE @datePrefix + '-%'
```

then inserts the new order with `OrderNumber = @datePrefix + '-' + FORMAT(seq, '00')`. The UNIQUE constraint `IX_ChangeOrders_OrderNumber` is the final safety net: on the rare race that two transactions read the same `MAX` before either commits, the second insert raises SQL Server error 2627 / 2601; the handler catches that specific error and retries up to N times (N=3) with a fresh sequence read.

**Rationale**: Database is the source of truth (constitution Principle IV). `UPDLOCK + HOLDLOCK` serializes readers within the same date prefix while not blocking other date prefixes. The UNIQUE constraint guarantees correctness even if the locking hint were missing or weakened. Retry-on-collision is bounded and observable.

**Alternatives considered**:
- **In-memory `Interlocked.Increment` per process** — fails in any multi-instance deployment and even single-instance on restart. Rejected.
- **Stored procedure with `MERGE`** — adds a deployment surface (sproc versioning) for negligible benefit over the plain transaction. Rejected.
- **Application-side optimistic concurrency token (`RowVersion`)** — solves a different problem (lost-update on edit), not the identity allocation problem.
- **Dedicated `OrderSequences` table keyed by date with `IDENTITY`/`SEQUENCE`** — works, but introduces a second moving part and an unnecessary FK relationship. The chosen approach uses the existing table.

**Open**: cap behavior at 99 entries/day. Documented in spec edge cases. Implementation will reject the 100th submission of a given day with `DomainErrors.Order.DailySequenceExhausted`.

---

## R-2 — `Idempotency-Key` storage

**Decision**: Dedicated table `dbo.IdempotencyKeys` co-located in the SQL Server database, retention 24 hours, scheduled cleanup job.

Schema:
- `Key` `nvarchar(64)` PK
- `OrderId` `uniqueidentifier NOT NULL` (FK → `ChangeOrders.Id`)
- `RequestHash` `varbinary(32)` (SHA-256 of canonicalized body) — used to detect divergent payloads with the same key
- `CreatedAt` `datetime2 NOT NULL DEFAULT SYSUTCDATETIME()`

Behavior on `POST /api/v1/change-orders` with `Idempotency-Key: X`:
1. If a row with `Key=X` exists AND `RequestHash` matches the incoming body → return the existing order with HTTP 200 (NOT 201).
2. If `Key=X` exists AND `RequestHash` differs → reject with HTTP 422 `DomainErrors.Idempotency.PayloadDivergence`.
3. Otherwise → insert order + idempotency row in the same transaction → return 201.

Cleanup: nightly hosted service (`IdempotencyCleanupService` : `BackgroundService`) deletes rows older than 24h.

**Rationale**: Closed via `/speckit-clarify` (see spec `## Clarifications` Q2). DB is the source of truth, transactional with order creation, no Redis dependency, deterministic across restarts.

**Alternatives considered**:
- `IDistributedCache` in-memory — loses state on Host restart. Rejected.
- `IDistributedCache` Redis — adds infra dependency in an on-premises deployment without other Redis users. Rejected.
- 7-day retention — larger BD footprint without observed retry behavior beyond hours. Rejected as default; can be tuned via config later.

---

## R-3 — `ApprovalChain` modeling: embedded vs related

**Decision**: Embedded value object on `ChangeOrder`. The four approval slots are flattened into four enum columns on `dbo.ChangeOrders`: `Approval_Requester`, `Approval_DepartmentHead`, `Approval_ItHead`, `Approval_ProgrammingDivision`, each `nvarchar(20) NOT NULL DEFAULT 'Pending'`.

**Rationale**: The chain has a fixed cardinality of 4 and the slot identities are part of the domain (not user-defined). Embedding avoids a join on every read and aligns with the constitution's preference for explicit, transparent modeling.

**Alternatives considered**:
- Separate `Approvals` table with `(OrderId, Level, Status, ChangedAt, ChangedBy)` rows — natural fit if approval **history** (who/when) is part of Fase 1. The spec does not list approval history as a requirement; the chain only carries the current verdict. Deferred to Fase 2 if history becomes a need.
- JSON column for `ApprovalChain` — opaque to SQL queries, harder to filter by "all orders pending IT head". Rejected.

---

## R-4 — Audit & soft-delete enforcement

**Decision**: A single `AuditInterceptor` implementing `ISaveChangesInterceptor` is registered in `Data` and is the only writer of `CreatedAt`, `UpdatedAt`, `IsDeleted`, `DeletedAt`. The interceptor intercepts `SavingChangesAsync` and walks `ChangeTracker.Entries()`:
- `Added` + `IAuditable` → sets `CreatedAt = UtcNow`.
- `Modified` + `IAuditable` → sets `UpdatedAt = UtcNow`.
- `Deleted` + `ISoftDeletable` → flips to `Modified` and sets `IsDeleted = true`, `DeletedAt = UtcNow`. **No row leaves through a physical delete**.

A global query filter `modelBuilder.Entity<ChangeOrder>().HasQueryFilter(e => !e.IsDeleted)` excludes soft-deleted rows from every default read.

**Rationale**: One enforcement point makes the audit contract auditable itself. Handlers cannot accidentally bypass the rules.

**Alternatives considered**:
- Explicit set in each handler — error-prone, scatters the rule. Rejected.
- DB triggers — moves the rule out of the application; harder to test. Rejected.
- DDD domain events with handlers — adds infrastructure (event bus) without operational gain. Rejected.

---

## R-5 — `Result<TValue, TError>` shape and `DomainErrors` catalog

**Decision**: Minimal generic record:

```csharp
public sealed record Result<TValue, TError>
{
    public TValue? Value { get; }
    public TError? Error { get; }
    public bool IsSuccess { get; }
    public static Result<TValue, TError> Success(TValue value) => new(value, default, true);
    public static Result<TValue, TError> Failure(TError error) => new(default, error, false);
    private Result(TValue? value, TError? error, bool isSuccess) { Value=value; Error=error; IsSuccess=isSuccess; }
}
```

`Error` is `public sealed record Error(string Code, string Message)`. The catalog `DomainErrors` is a single static class with nested static classes per aggregate:

```csharp
public static class DomainErrors
{
    public static class Order
    {
        public static Error NotFound(Guid id) => new("order.not_found", $"Order {id} not found.");
        public static Error DuplicateNumber(string number) => new("order.duplicate_number", $"OrderNumber {number} already exists.");
        public static Error InvalidStateTransition(OrderStatus from, OrderStatus to) => new("order.invalid_transition", $"Cannot move from {from} to {to}.");
        public static Error EditAfterDraft() => new("order.edit_after_draft", "PUT is only allowed while the order is in Draft (FR-006).");
        public static Error DailySequenceExhausted(DateOnly date) => new("order.daily_sequence_exhausted", $"More than 99 orders requested for {date:yyyy-MM-dd}.");
    }
    public static class Idempotency
    {
        public static Error PayloadDivergence(string key) => new("idempotency.payload_divergence", $"Idempotency-Key {key} was previously used with a different payload.");
    }
}
```

**Rationale**: A small handful of error builders covers ~all known failure paths; the `Code` lets the HTTP layer (`ProblemDetails`) translate uniformly.

**Alternatives considered**:
- `OneOf<TSuccess, TError>` from the `OneOf` NuGet package — third-party dep for marginal ergonomic gain. Rejected per "composition over inheritance, manual mapping" mindset (we don't want runtime-magic-tagged unions).
- Throwing custom exceptions — violates Principle III. Rejected.
- Multiple Error subtypes by category — over-engineered; flat `Error(Code, Message)` is enough.

---

## R-6 — Mapping strategy

**Decision**: Manual static extension classes, one per aggregate:

```csharp
internal static class OrderMapper
{
    public static OrderResponse ToResponse(this ChangeOrder o) => new(/* explicit field-by-field assignment */);
    public static IReadOnlyList<OrderResponse> ToResponseList(this IEnumerable<ChangeOrder> orders) => orders.Select(ToResponse).ToList();
    public static ChangeOrder ToEntity(this CreateOrderRequest req, OrderNumber number) => new(/* ctor */);
}
```

**Rationale**: Mapping is the most-changed code in any non-trivial feature; reading and reviewing manual code is straightforward. Constitution Principle V is non-negotiable on this.

**Alternatives considered**: AutoMapper / Mapster — explicitly forbidden by the constitution.

---

## R-7 — Rate limiting

**Decision**: Use ASP.NET Core 10's built-in `Microsoft.AspNetCore.RateLimiting` with a **fixed window** of 1 minute and a permit count of 100. Partition key derived from the client identifier (`HttpContext.Connection.RemoteIpAddress` in Fase 1; replaced by authenticated principal in Fase 2). On rejection: HTTP 429 with `Retry-After` header reflecting the time until the next window opens.

**Rationale**: Built-in middleware, zero new deps, matches the spec's SC-005 latency budget.

**Alternatives considered**:
- Token bucket — smoother but harder to communicate to internal-only clients used to "you get 100 per minute". Rejected for Fase 1.
- AspNetCoreRateLimit (third-party) — predates the built-in; redundant on .NET 10. Rejected.

---

## R-8 — OpenAPI 3.1 generation and the C# 12 interceptors gotcha

**Decision**: Use `Microsoft.AspNetCore.OpenApi` (built into .NET 10). `Host.csproj` MUST declare `<InterceptorsNamespaces>$(InterceptorsNamespaces);Microsoft.AspNetCore.OpenApi.Generated</InterceptorsNamespaces>` because the XML-comment source generator emits code that uses C# 12 interceptors — without the allowlist the build fails with `CS9137`.

**Rationale**: Discovered earlier in this repository (engram observation `dotnet restore falla por HTTP/2 ALPN en este entorno` series). Documenting it here ensures Phase 2 implementation does not rediscover it the hard way.

**Alternatives considered**: Swashbuckle — older, separate code generator, no integration with the C# 14 toolchain. Rejected for new projects on .NET 10.

---

## R-9 — Testing topology

**Decision**:
- `ChangeOrder.Domain.Tests` — pure unit tests of value objects (`OrderNumber.Create` format guard) and `DomainErrors` factories. No infra.
- `ChangeOrder.Business.Tests` — handler tests with `NSubstitute` mocks for `IChangeOrderRepository` and `IUnitOfWork`. Focus: command/query Behavior, validation, Result-Pattern emission.
- `ChangeOrder.Data.Tests` — integration tests with EF Core In-Memory provider for the simple cases, and **Testcontainers SQL Server** for the concurrency tests (R-1 collision retry).
- `ChangeOrder.Presentation.Tests` — `WebApplicationFactory<TEntryPoint>` based end-to-end tests against the in-process Host with the EF Core In-Memory provider (except the OrderNumber collision test that needs real SQL).

**Rationale**: The unit/integration split matches the layer responsibilities. Testcontainers is reserved for tests that the In-Memory provider cannot model (transactions with `UPDLOCK/HOLDLOCK`, UNIQUE constraint).

**Alternatives considered**: Single test project — violates the constitution's per-layer organization. Rejected.

---

## R-10 — Local environment workaround (HTTP/2 ALPN against NuGet)

**Decision**: Document in the project README and in the planning artifacts that **on the current dev host** `dotnet restore/build/run` requires:

```bash
export DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP2SUPPORT=false
export DOTNET_SYSTEM_NET_DISABLEIPV6=1
```

This is a host-level issue (HTTP/2 ALPN filtered), not a project-level issue. CI runners (GitHub Actions ubuntu-latest) are not affected and do not need these variables — but if a new dev machine reproduces the issue, the workaround is in `Docs/0-Initial/plan.md` and `CLAUDE.md`.

**Rationale**: Saves the next contributor an hour of debugging.

**Alternatives considered**: Fix the host config — out of scope for the project, depends on corporate network.
