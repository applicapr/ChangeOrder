# Feature Specification: Change Order Management

**Feature Branch**: `001-change-order-management`  
**Created**: 2026-05-12  
**Status**: Draft  
**Input**: User description: "Sistema CRUD de Change Order Management con cadena de 4 aprobaciones, OrderNumber thread-safe yyyyMMdd-##, soft delete, auditoría, idempotencia POST, paginación y rate limiting. Detalles completos en Docs/0-Initial/spec.md y plan técnico en Docs/0-Initial/plan.md."

## Clarifications

### Session 2026-05-12

- Q: For Fase 1, which authorization matrix does `PUT /change-orders/{id}` enforce? → A: Defer per-role matrix to Fase 2. In Fase 1, `PUT` is allowed only while the order is in `Draft`; once the order is in `PendingApproval` or later, `PUT` is rejected (HTTP 409 Conflict) — only the workflow-advancement endpoints (approval, delivery date, deploy date, post-deploy screenshot) may mutate the order.
- Q: Where and for how long does the system persist `Idempotency-Key` values? → A: Dedicated SQL Server table (`IdempotencyKeys`) in the same database as orders. Retention window is 24 hours; entries older than that are eligible for deletion by a scheduled cleanup job. No external cache (Redis) is introduced.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Capture a change request and assign it a unique order number (Priority: P1)

A staff member identifies a needed change in a production application (e.g., "fix the calculation of withholding in module X version 4.7.2") and records the request in the system. As soon as the request is submitted, the system stamps it with a unique, human-readable order number formatted `yyyyMMdd-##` (e.g., `20260512-01`), so the request is traceable end-to-end from this point forward.

**Why this priority**: Without this story, no other operation makes sense. It is the **minimum viable product** — the system delivers value the moment a request stops living in email and starts living as a numbered record.

**Independent Test**: A user can submit a complete change request through the API and immediately receive back the new order number plus the full record, persisted with status `Draft`. Submitting two requests on the same business day produces two sequential numbers (`yyyyMMdd-01`, `yyyyMMdd-02`).

**Acceptance Scenarios**:

1. **Given** the requester has the affected program info (name, current production version, pre-change screenshot), the requested work, justification, and the action required, **When** the requester submits the change request, **Then** the system creates the order with status `Draft`, generates an `OrderNumber` of the form `yyyyMMdd-##` for today's UTC date with the next sequence number, and returns the order back to the requester.
2. **Given** two requesters submit change requests within milliseconds of each other on the same day, **When** both submissions are processed, **Then** each receives a distinct `OrderNumber` and neither submission fails because of the collision.
3. **Given** the requester submitted a request 10 seconds ago and clicks "submit" again because of a network retry (same `Idempotency-Key`), **When** the second submission arrives, **Then** the system returns the original order without creating a duplicate.
4. **Given** a submission missing a mandatory field (e.g., `Justification`), **When** it arrives at the system, **Then** the system rejects it with a clear validation error and does not consume an `OrderNumber`.

---

### User Story 2 - Move the request through the four-level approval chain (Priority: P2)

A change order in `Draft` is sent for evaluation. Four approvers — the original Requester, the Department Head, the IT Head, and the Programming Division — each independently mark their approval as Approved or Rejected. The order can only advance to `Approved` once all four levels are Approved; any rejection blocks progress until corrected.

**Why this priority**: This is the core governance loop of the system. Without it the request is just a record; with it the record becomes the auditable approval trail the organization relies on for compliance.

**Independent Test**: A user can take an existing `Draft` order, register a verdict (Approve or Reject) at each of the four levels through the API, and observe the order status transition from `Draft → PendingApproval → Approved` once all four are Approved, or stay blocked if any level is Rejected.

**Acceptance Scenarios**:

1. **Given** an order in `Draft`, **When** the system records the Requester's self-confirmation, **Then** the order moves to `PendingApproval` and the Requester's approval status becomes `Approved`.
2. **Given** an order in `PendingApproval` with three of four approvals at `Approved` and one at `Pending`, **When** the remaining approver marks their level `Approved`, **Then** the order transitions to `Approved`.
3. **Given** an order in `PendingApproval`, **When** any approver marks their level `Rejected`, **Then** the order remains in `PendingApproval` (does not advance to `Approved`) and the rejection is recorded with timestamp.
4. **Given** an `Approved` order, **When** the work begins, **Then** the order transitions to `InProgress`.
5. **Given** an `InProgress` order, **When** the deployment to production is recorded with the post-deployment screenshot and date, **Then** the order transitions to `Deployed`.

---

### User Story 3 - List, search and maintain existing orders (Priority: P3)

Operators need to browse the catalog of change orders, look up a specific one by its ID, update fields on an in-progress order, or soft-delete an order that was opened in error.

**Why this priority**: Once orders exist and flow through approvals, the day-to-day operation of the system requires listing, lookup, edits and removals. This is the maintenance surface — necessary but secondary to creation and approvals.

**Independent Test**: A user can request a paginated list of orders (page 1, size 10), retrieve a specific order by ID, update a mutable field on an order that is still editable, and "delete" an order (which actually marks it soft-deleted; subsequent listings exclude it).

**Acceptance Scenarios**:

1. **Given** 35 orders exist, **When** the user requests page 2 with size 10, **Then** the system returns orders 11–20 along with totals (`TotalCount=35`, `Page=2`, `PageSize=10`).
2. **Given** an order in `Draft` or `PendingApproval`, **When** the user updates the justification, **Then** the change is persisted and the order's "updated" timestamp reflects the modification.
3. **Given** an order in `Approved`, `InProgress` or `Deployed`, **When** the user attempts to modify the work description, **Then** the system rejects the change with an explanation grounded in the order's current state and the actor's role (the precise matrix is defined under FR-006).
4. **Given** an order exists, **When** the user "deletes" it, **Then** the order disappears from default listings and lookups but remains physically present in the system for audit purposes.

---

### Edge Cases

- **Date boundary at midnight UTC**: a submission that arrives at 23:59:59 of day D and one that arrives at 00:00:00 of day D+1 must get sequence numbers anchored to their respective UTC date prefixes, never crossing the boundary.
- **Sequence exhaustion**: the format reserves only two digits for the daily sequence (`##`). The system MUST handle, or explicitly cap, the unlikely case of >99 orders in a single day.
- **Idempotency-Key collision across orders**: two genuinely different requests submitted with the same `Idempotency-Key` (e.g., a buggy client reusing the key) must NOT be silently merged; the system must detect divergent payloads and reject the second submission with a clear error.
- **Rate limit exhaustion**: when a single client breaches the 100 req/min ceiling, the 101st request returns HTTP 429 with a clear retry-after signal; in-flight legitimate requests are NOT terminated mid-flight.
- **Re-submission of a rejected approval**: after an approver marks `Rejected`, the spec must define whether the order is reset to `Draft` for correction or remains in `PendingApproval` awaiting an explicit "reopen" action. (Decision documented in Assumptions.)
- **Concurrent modification**: two operators editing the same order simultaneously — the second writer MUST see a conflict signal (HTTP 409), not a silent overwrite. Mechanism: optimistic concurrency token `RowVersion` (see FR-013).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST accept a complete change-request submission containing: affected program name, production version, pre-change screenshot reference, work description, request details, justification, required action, and requester contact information.
- **FR-002**: System MUST generate a unique `OrderNumber` of the form `yyyyMMdd-##` per submission, where the date prefix is the submission's UTC date and `##` is the next available daily sequence. Two submissions on the same day MUST receive different numbers even under simultaneous load.
- **FR-003**: System MUST start every new order in status `Draft` and MUST enforce these state transitions only: `Draft → PendingApproval`, `PendingApproval → Approved | Cancelled`, `Approved → InProgress`, `InProgress → Deployed | Cancelled`. Any other transition MUST be rejected.
- **FR-004**: System MUST record four independent approval verdicts (Requester, Department Head, IT Head, Programming Division), each as one of `Pending | Approved | Rejected`, and MUST refuse to mark the order `Approved` until all four are `Approved`.
- **FR-005**: System MUST support reading the full record of any order by its identifier and MUST support listing orders with mandatory pagination: caller specifies `Page ≥ 1` and `PageSize` in `[1..50]`; response carries `Items`, `TotalCount`, `Page`, `PageSize`.
- **FR-006**: System MUST allow `PUT /change-orders/{id}` to modify any field of the order ONLY while the order is in `Draft`. Once the order is in `PendingApproval`, `Approved`, `InProgress`, `Deployed`, or `Cancelled`, `PUT` MUST be rejected with HTTP 409 Conflict and a descriptive error. Workflow-advancement endpoints (record approval verdict, record delivery date, record initial evaluation date, record production deploy date, attach post-deploy screenshot) are the only sanctioned ways to mutate an order past `Draft` in Fase 1. The finer-grained per-role authorization matrix is deferred to Fase 2 once identity-aware authentication is in place.
- **FR-007**: System MUST support a "delete" operation that performs a soft delete (record marked deleted, NOT physically removed) and MUST exclude soft-deleted orders from default listings and single-id lookups.
- **FR-008**: System MUST honor an `Idempotency-Key` header on order-creation requests so that retried submissions return the original order rather than creating duplicates.
- **FR-009**: System MUST enforce a rate-limit ceiling of 100 requests per minute per client (fixed window) and return HTTP 429 with retry-after guidance when exceeded.
- **FR-010**: System MUST expose a health-check endpoint that verifies database connectivity and reports a clear pass/fail signal usable by infrastructure monitoring.
- **FR-011**: System MUST log every operation in a structured form suitable for audit replay, and MUST NOT log personally identifying or sensitive content in plaintext.
- **FR-012**: System MUST persist `Idempotency-Key` values in a dedicated database table (same SQL Server instance as the orders) with a retention window of 24 hours. Within the retention window, a repeated `POST` carrying the same key returns the previously created order without duplicating it; outside the window, the key is treated as fresh. A scheduled cleanup job MUST evict entries older than the retention window.
- **FR-013**: System MUST detect concurrent modifications to the same order via an optimistic concurrency token (`RowVersion`). Every read response MUST include the current token. **Scope of enforcement**: only `PUT /api/v1/change-orders/{id}` requires the client to echo the token — when the submitted token does not match the persisted one the system MUST reject the write with **HTTP 409 Conflict** (`order.concurrency_conflict`). The workflow-advancement endpoints (`PUT /{id}/approvals/{level}` and `PATCH /{id}/dates`) are **exempt** because each writes to an independent slot of the aggregate (one approval level / one milestone date); requiring a token on those endpoints would force the four approvers to act sequentially, which contradicts the design intent of an independent four-level approval chain. (Added during `/speckit-analyze` 2026-05-12, finding F2; scope clarified in finding NEW1.)

### Audit & Soft-Delete Impact *(mandatory when feature touches persisted entities)*

Per `.specify/memory/constitution.md` v1.0.0 Principle IV, the feature touches persisted entities; therefore:

- **AS-001**: The `ChangeOrder` aggregate root implements both `ISoftDeletable` (`IsDeleted`, `DeletedAt`) and `IAuditable` (`CreatedAt`, `UpdatedAt`). No handler writes these fields manually — the global `AuditInterceptor` is the only writer.
- **AS-002**: `OrderNumber` is a business identifier and MUST carry a UNIQUE constraint at the database level. Application-side collision checks are advisory only; the database is the source of truth.
- **AS-003**: All listing and single-record read paths honor the global `HasQueryFilter` for soft delete. No read path bypasses the filter; there is no current requirement to expose soft-deleted records.
- **AS-004**: The `DELETE` endpoint sets `IsDeleted=true` and `DeletedAt=UtcNow` on the row; it never issues a physical `DELETE`. The post-delete state remains visible to audit queries that explicitly opt out of the filter (future capability, not part of this spec).

### Key Entities

- **ChangeOrder** (aggregate root): represents one production-change request. Attributes include the unique `OrderNumber`, affected program info, request details and justification, the four-level approval chain, milestone dates (delivery, initial evaluation, production deploy), pre- and post-change screenshot references, status, soft-delete flags and audit timestamps.
- **RequesterInfo** (value object embedded in ChangeOrder): identifies the human who originated the request — name, organizational position, department, contact email. Treated as immutable once the order is created.
- **ApprovalChain** (value object embedded in ChangeOrder): the four independent approval slots — Requester, Department Head, IT Head, Programming Division — each carrying its own `ApprovalStatus`. The chain is the authoritative governance record of the order.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100 simultaneously submitted change requests on the same calendar day all receive distinct order numbers and zero submissions fail because of a numbering collision.
- **SC-002**: An end-user can complete a change-request submission and see the new order number returned in under 3 seconds, end to end, under normal load.
- **SC-003**: An identical retry (same `Idempotency-Key`, same payload) submitted within the retention window returns the original order with no duplicate persisted; this is verifiable by counting orders before and after the retry.
- **SC-004**: A "deleted" order disappears from every default listing and single-id lookup but remains physically present and retrievable through an auditor's tooling, demonstrating compliance with the no-physical-delete policy.
- **SC-005**: The 101st request from a single client within a 60-second window receives HTTP 429 within 50 ms of arrival; legitimate clients staying below the ceiling are never throttled.
- **SC-006**: An order cannot transition into `Approved` until each of the four independent approval slots is `Approved`; this rule fires 100% of the time, evidenced by automated tests covering every illegal transition combination.
- **SC-007**: The health-check endpoint flips from healthy to unhealthy within 5 seconds of a database outage and back within 5 seconds of recovery, allowing infrastructure monitoring to react.

## Assumptions

- Authentication and authorization for **Fase 1** rely on network-level isolation: the API runs on internal corporate infrastructure with CORS limited to internal hosts and no public internet exposure. Identity-aware authorization (which user holds which role) is **out of scope** for Fase 1; the per-state authorization matrix (FR-006) will be defined and implemented once identity is available.
- Notifications to approvers when their attention is required are **out of scope** for Fase 1; approvers poll the system. Email or push notifications are planned for Fase 2.
- Document generation per order (e.g., a PDF of the order with all signatures) is **out of scope** for Fase 1; planned for Fase 2 with the WPF client.
- A rejection at any approval level keeps the order in `PendingApproval` and surfaces the rejected level; the business correction loop is "edit the order while still in `PendingApproval`, then re-request approval at the rejected level" rather than resetting the entire chain.
- The two-digit daily sequence (`##`) is assumed sufficient: historical operations data shows fewer than 30 orders per business day. The system MUST still detect and reject the >99 case rather than silently overflow.
- All timestamps in the API contract are UTC; the date prefix of `OrderNumber` is computed from UTC, never from a local time zone.
