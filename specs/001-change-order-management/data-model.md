# Phase 1 — Data Model: Change Order Management

**Feature**: `001-change-order-management`
**Date**: 2026-05-12

This document describes the persisted entities, their attributes, relationships, validation rules, and state transitions. All decisions trace back to `spec.md` (Functional Requirements + Audit & Soft-Delete Impact) and `research.md`.

---

## 1. Aggregate root — `ChangeOrder`

| Attribute | Type | Constraints | Source |
|---|---|---|---|
| `Id` | `Guid` | PK, clustered | RF-1 |
| `OrderNumber` | `OrderNumber` (VO) | UNIQUE, format `yyyyMMdd-##`, `varchar(13)` in DB | FR-2, AS-002, R-1 |
| `ProgramName` | `string` | NOT NULL, ≤ 200 chars | RF-1 |
| `ProductionVersion` | `string` | NOT NULL, ≤ 50 chars | RF-1 |
| `VersionScreenshotPath` | `string` | nullable, ≤ 500 chars (file reference, not file blob) | RF-1 |
| `RequestDate` | `DateTime` (UTC) | NOT NULL, indexed | RF-1 |
| `Requester` | `RequesterInfo` (VO) | flattened to 4 columns; see §3 | RF-1 |
| `WorkDescription` | `string` | NOT NULL, ≤ 2000 chars | RF-1 |
| `RequestDetails` | `string` | NOT NULL, ≤ 4000 chars | RF-1 |
| `Justification` | `string` | NOT NULL, ≤ 2000 chars | RF-1 |
| `RequiredAction` | `string` | NOT NULL, ≤ 1000 chars | RF-1 |
| `ApprovalChain` | `ApprovalChain` (VO) | flattened to 4 enum columns; see §4 | RF-2, FR-4 |
| `Status` | `OrderStatus` (enum) | NOT NULL DEFAULT `Draft`, indexed | RF-3, FR-3 |
| `DeliveryDate` | `DateTime?` (UTC) | nullable | RF-5 |
| `InitialEvaluationDate` | `DateTime?` (UTC) | nullable | RF-5 |
| `ProductionDeployDate` | `DateTime?` (UTC) | nullable | RF-5 |
| `PostDeployScreenshotPath` | `string?` | nullable, ≤ 500 chars | RF-5 |
| `CreatedAt` | `DateTime` (UTC) | NOT NULL, set by `AuditInterceptor` | AS-001 |
| `UpdatedAt` | `DateTime?` (UTC) | nullable, set by `AuditInterceptor` | AS-001 |
| `IsDeleted` | `bool` | NOT NULL DEFAULT 0, indexed | AS-001, AS-004 |
| `DeletedAt` | `DateTime?` (UTC) | nullable, set by `AuditInterceptor` | AS-001, AS-004 |

**Implements**: `ISoftDeletable`, `IAuditable`.
**Constructor**: explicit (no parameterless). EF Core 10 uses a backing-field constructor for materialization.
**Mutation surface**: handlers do not set audit/soft-delete fields directly — `AuditInterceptor` is the only writer (Principle IV).

### Indexes

- `IX_ChangeOrders_OrderNumber` — UNIQUE — race-safety net for R-1.
- `IX_ChangeOrders_RequestDate` — non-clustered.
- `IX_ChangeOrders_Status` — non-clustered.
- `IX_ChangeOrders_IsDeleted` — non-clustered, supports the global query filter.

### Global query filter

```csharp
modelBuilder.Entity<ChangeOrder>().HasQueryFilter(o => !o.IsDeleted);
```

---

## 2. Value object — `OrderNumber`

```csharp
public sealed record OrderNumber
{
    public string Value { get; }

    private OrderNumber(string value) { Value = value; }

    public static Result<OrderNumber, Error> Create(DateOnly date, int sequence)
    {
        if (sequence is < 1 or > 99)
            return Result<OrderNumber, Error>.Failure(DomainErrors.Order.DailySequenceExhausted(date));
        return Result<OrderNumber, Error>.Success(
            new OrderNumber($"{date:yyyyMMdd}-{sequence:00}"));
    }

    public override string ToString() => Value;
}
```

- **Format**: `yyyyMMdd-##` exactly 11 characters; `varchar(13)` in DB to allow future widening without migration.
- **UNIQUE** at the DB level (`IX_ChangeOrders_OrderNumber`).
- **Construction is closed**: only via `Create(date, sequence)` factory; the constructor is `private`.
- **Equality**: record-based value equality on `Value`.

---

## 3. Value object — `RequesterInfo`

```csharp
public sealed record RequesterInfo(
    string Name,
    string Position,
    string Department,
    string Email);
```

**Columns (flattened on `ChangeOrders`)**:

| Column | Type | Validation |
|---|---|---|
| `Requester_Name` | `nvarchar(150) NOT NULL` | non-empty |
| `Requester_Position` | `nvarchar(100) NOT NULL` | non-empty |
| `Requester_Department` | `nvarchar(100) NOT NULL` | non-empty |
| `Requester_Email` | `nvarchar(200) NOT NULL` | RFC-5322 surface validation (no DNS lookup) |

**Immutability**: this VO is captured at order creation time. Subsequent edits to "Requester contact info" must be modeled as a separate audit-trail entry, not as an in-place mutation (out of scope for Fase 1).

---

## 4. Value object — `ApprovalChain`

```csharp
public sealed record ApprovalChain(
    ApprovalStatus RequesterApproval,
    ApprovalStatus DepartmentHeadApproval,
    ApprovalStatus ItHeadApproval,
    ApprovalStatus ProgrammingDivisionApproval);
```

**Columns (flattened on `ChangeOrders`)**:

| Column | Type |
|---|---|
| `Approval_Requester` | `nvarchar(20) NOT NULL DEFAULT 'Pending'` |
| `Approval_DepartmentHead` | `nvarchar(20) NOT NULL DEFAULT 'Pending'` |
| `Approval_ItHead` | `nvarchar(20) NOT NULL DEFAULT 'Pending'` |
| `Approval_ProgrammingDivision` | `nvarchar(20) NOT NULL DEFAULT 'Pending'` |

Stored as strings (enum names) for human-readability in raw SQL queries. Validated to be exactly one of `Pending | Approved | Rejected`.

**Invariant**: `Status` transitions to `Approved` (`OrderStatus.Approved`) only when **all four** approval columns are `Approved`. This invariant is enforced in the handler that advances workflow, not at the DB level (the constraint would be too complex; the rule belongs in the domain).

---

## 5. Enums

```csharp
public enum OrderStatus
{
    Draft,
    PendingApproval,
    Approved,
    InProgress,
    Deployed,
    Cancelled
}

public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected
}
```

Stored as `nvarchar(20)` for readability. EF Core conversion: `HasConversion<string>()`.

---

## 6. Technical entity — `IdempotencyKey`

| Attribute | Type | Constraints |
|---|---|---|
| `Key` | `string` | PK, `nvarchar(64)` |
| `OrderId` | `Guid` | FK → `ChangeOrders.Id`, NOT NULL |
| `RequestHash` | `byte[]` | `varbinary(32)` (SHA-256), NOT NULL |
| `CreatedAt` | `DateTime` (UTC) | NOT NULL DEFAULT `SYSUTCDATETIME()` |

**Implements**: nothing — it is intentionally **not** auditable nor soft-deletable. The cleanup job hard-deletes expired rows.

**Index**: `IX_IdempotencyKeys_CreatedAt` to speed up the cleanup query (`DELETE FROM IdempotencyKeys WHERE CreatedAt < @cutoff`).

**Cleanup**: `IdempotencyCleanupService : BackgroundService` runs every hour, deleting rows older than 24 hours.

**Why exempt from audit/soft-delete?** Constitution Principle IV applies to **persisted business entities**. Idempotency keys are infrastructure plumbing with a sunset clock; keeping them indefinitely defeats the cleanup design. Documented exception in `research.md` R-2.

---

## 7. Relationships

```
ChangeOrder  (1) ──── (0..1)  IdempotencyKey
```

- One change order may have at most one `IdempotencyKey` row (one POST creation request can be retried with the same key).
- An idempotency key always points to exactly one order (the one that the original successful POST produced).
- A soft-deleted order keeps its idempotency row until the cleanup job evicts it; this is fine because the global query filter excludes the order from reads regardless.

---

## 8. State transitions on `ChangeOrder.Status`

```
                          ┌────────────┐
                          │   Draft    │ ◀── initial
                          └─────┬──────┘
                                │ submit-for-approval
                                ▼
                          ┌────────────┐
        ┌───── cancel ────│ PendingAp. │
        │                 └─────┬──────┘
        │                       │ all-four-approved
        │                       ▼
        │                 ┌────────────┐
        │                 │  Approved  │
        │                 └─────┬──────┘
        │                       │ begin-work
        │                       ▼
        │                 ┌────────────┐
        ├───── cancel ────│ InProgress │
        │                 └─────┬──────┘
        │                       │ deploy-recorded
        │                       ▼
        │                 ┌────────────┐
        │                 │  Deployed  │
        │                 └────────────┘
        ▼
┌────────────┐
│ Cancelled  │
└────────────┘
```

Transitions explicitly forbidden by FR-3:
- Any backwards transition (e.g., `Deployed → InProgress`).
- `Cancelled → *` (cancellation is terminal).
- `Draft → Approved` without going through `PendingApproval`.

Enforced in the `AdvanceWorkflowHandler` via a small state machine table.

---

## 9. Validation rules summary

| Field / Rule | Where validated | Behavior on violation |
|---|---|---|
| `OrderNumber` format | `OrderNumber.Create` | `DomainErrors.Order.DailySequenceExhausted` |
| `OrderNumber` uniqueness | DB UNIQUE constraint + handler retry | retry up to 3 times then `DomainErrors.Order.DuplicateNumber` |
| `Requester.Email` shape | `CreateOrderValidator` (FluentValidation-like via .NET 10 `AddValidation()`) | `ProblemDetails` 400 |
| Required text fields non-empty | validator | 400 |
| `OrderStatus` transitions | `AdvanceWorkflowHandler` | `DomainErrors.Order.InvalidStateTransition` |
| All 4 approvals = Approved before `Approved` status | `AdvanceWorkflowHandler` | rejected with explicit error |
| `PUT` only allowed in `Draft` | `UpdateOrderHandler` | `DomainErrors.Order.EditAfterDraft` → HTTP 409 |
| `Idempotency-Key` payload divergence | `IdempotencyService` | `DomainErrors.Idempotency.PayloadDivergence` → HTTP 422 |
| Soft delete cannot resurrect | `Delete` is final via interceptor | no API path to undelete |

---

## 10. Migration plan (Phase 2 = `/speckit-implement`)

The initial EF Core migration `InitialCreate` MUST:
1. Create `dbo.ChangeOrders` with all columns and the 4 indexes above.
2. Create `dbo.IdempotencyKeys` with its PK and index.
3. Add the `OrderNumber` UNIQUE constraint with name `IX_ChangeOrders_OrderNumber` (not the EF default, for readability in error messages).

No data seed in this migration; tests provide their own fixtures.
