# Data Model Quality Checklist: Change Order Management

**Purpose**: Validate that the data-model requirements (in `spec.md`, `data-model.md`, and the entities described in `research.md`) are complete, clear, consistent, and measurable. This is a "unit test for English" — it tests how the requirements are WRITTEN, not whether the EF Core code maps them correctly.
**Created**: 2026-05-12
**Feature**: [spec.md](../spec.md), [data-model.md](../data-model.md)

## Entity Completeness

- [ ] CHK001 Are all aggregate roots, value objects and technical entities introduced by this feature listed in `data-model.md`? [Completeness]
- [ ] CHK002 Is the rationale documented for which types are entities vs value objects? [Clarity, data-model.md §1–6, research.md R-3]
- [ ] CHK003 Are `IdempotencyKey`'s exemption from `ISoftDeletable` and `IAuditable` explicitly justified somewhere in the documentation? [Clarity, data-model.md §6, research.md R-2]

## Attribute & Column Constraints

- [ ] CHK004 Does every persisted column declare its SQL type, nullability and length explicitly (no implicit `nvarchar(MAX)`)? [Clarity, data-model.md §1, §3, §4, §6]
- [ ] CHK005 Are validation rules for free-text fields (e.g., `WorkDescription ≤ 2000 chars`) reflected in both `data-model.md` and the `CreateOrderRequest` schema in `openapi.yaml`? [Consistency]
- [ ] CHK006 Is the `Requester_Email` validation surface specified (RFC 5322 surface check, no DNS resolution)? [Clarity, data-model.md §3]

## Identity, Uniqueness, Indexing

- [ ] CHK007 Is the UNIQUE constraint on `OrderNumber` named explicitly (`IX_ChangeOrders_OrderNumber`) rather than left to EF Core defaults? [Clarity, data-model.md §1]
- [ ] CHK008 Are the four non-clustered indexes (`RequestDate`, `Status`, `IsDeleted`, plus the unique on `OrderNumber`) tied to specific query workloads? [Traceability, Gap]
- [ ] CHK009 Are FK relationships (`IdempotencyKey.OrderId → ChangeOrder.Id`) declared with their cascade behavior (no cascade? restrict? set-null?)? [Gap, data-model.md §7]

## State Transitions

- [ ] CHK010 Is every transition in the `OrderStatus` diagram (`Draft → PendingApproval`, `PendingApproval → Approved | Cancelled`, `Approved → InProgress`, `InProgress → Deployed | Cancelled`) covered by at least one acceptance scenario in the spec? [Coverage, data-model.md §8, Spec §User Stories]
- [ ] CHK011 Are all FORBIDDEN transitions enumerated (e.g., `Deployed → InProgress`, `Cancelled → *`)? [Coverage, data-model.md §8]
- [ ] CHK012 Does the spec say what happens to a `Cancelled` order: is the soft delete or only the status change? [Clarity, Gap]

## Value Object Invariants

- [ ] CHK013 Is the `OrderNumber.Create(date, sequence)` validation (sequence ∈ [1..99]) documented as a hard invariant rather than a recommendation? [Clarity, data-model.md §2]
- [ ] CHK014 Is the immutability of `RequesterInfo` after order creation documented as a domain rule, not only as an implementation choice? [Clarity, data-model.md §3]
- [ ] CHK015 Is the `ApprovalChain` invariant ("`Status=Approved` only when all four slots are `Approved`") stated where the data model lives, not only inside the handler? [Consistency, data-model.md §4]

## Audit & Soft-Delete Invariants

- [ ] CHK016 Is the rule "handlers do not write audit/soft-delete columns directly" documented as a contract that reviewers can verify by inspection? [Clarity, data-model.md §1, research.md R-4]
- [ ] CHK017 Are the global query filter and the "no API path to undelete" rule traced to a specific success criterion in the spec? [Traceability, Spec §SC-004]

## Edge Cases & Boundaries

- [ ] CHK018 Is the daily sequence exhaustion case (>99 orders/day) explicitly addressed with a domain error (`DomainErrors.Order.DailySequenceExhausted`) rather than left to overflow? [Edge Case, research.md R-1]
- [ ] CHK019 Is the midnight-UTC boundary defined (a submission at 23:59:59 UTC uses today's prefix; 00:00:00 uses tomorrow's)? [Edge Case, Spec §Edge Cases]
- [ ] CHK020 Are concurrent edits to the same order (optimistic concurrency token vs last-write-wins) addressed in the model? [Gap, Spec §Edge Cases]

## Migrations

- [ ] CHK021 Is the initial migration's responsibility scope documented (creates tables + UNIQUE + indexes; no seed)? [Clarity, data-model.md §10]
- [ ] CHK022 Are downgrade / rollback expectations for migrations documented? [Gap]

## Notes

- These items test the **written model**, not running EF Core code.
- A `[Gap]` marker means: the spec/data-model is silent on this; deciding it requires a project decision before implementation.
