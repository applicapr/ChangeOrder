# Specification Quality Checklist: Change Order Management

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-05-12
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] **No implementation details** (languages, frameworks, APIs) — *with caveat documented in Notes*
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders — *user stories are; the Audit & Soft-Delete Impact section is intentionally technical*
- [x] All mandatory sections completed

## Requirement Completeness

- [ ] **No [NEEDS CLARIFICATION] markers remain** — **2 markers intentionally retained** (within the ≤3 cap mandated by the SKILL.md, both representing genuine business gaps; see Notes)
- [x] Requirements are testable and unambiguous (except the two explicitly flagged)
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic — *HTTP status codes are treated as API contract, not technology choice*
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified (6 listed)
- [x] Scope is clearly bounded (4 explicit out-of-scope items in Assumptions)
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria — *covered transitively through User Stories' acceptance scenarios*
- [x] User scenarios cover primary flows — *Create (P1) + Approve (P2) + Maintain (P3) form the complete CRUD + governance loop*
- [x] Feature meets measurable outcomes defined in Success Criteria
- [ ] **No implementation details leak into specification** — **deliberate exception**: the Audit & Soft-Delete Impact section is mandated by `.specify/memory/constitution.md` v1.0.0 Principle IV and is technical by design

## Notes

### Open clarifications (carry forward to `/speckit-clarify` or `/speckit-plan`)

| ID | Where | Question |
|---|---|---|
| C-1 | FR-006 / US3 Scenario 3 | Exact per-state authorization matrix: which fields are mutable in which `OrderStatus`, and which actor(s) may mutate them. |
| C-2 | FR-012 | Idempotency-key storage mechanism (database table vs `IDistributedCache`) and retention window (24h vs 7d vs other). |

Both gaps are genuine — they have no defensible default that wouldn't lock in a major architectural commitment prematurely. They are deliberately deferred to `/speckit-clarify` (preferred) or to the planning phase.

### Deliberate exceptions to "no implementation details"

| Section | Detail | Why retained |
|---|---|---|
| Audit & Soft-Delete Impact (AS-001..AS-004) | Mentions `ISoftDeletable`, `IAuditable`, `AuditInterceptor`, `HasQueryFilter`, UNIQUE constraint | The section is **mandated** by the constitution (Principle IV) and serves as the auditable contract that the implementation will respect these invariants. Removing it would weaken the gate that the constitution enshrines. |
| FR-008, FR-009, SC-005 | `Idempotency-Key` HTTP header, HTTP 429 status code | Treated as part of the API **contract surface**, not the implementation. The spec defines the protocol the API speaks; the plan defines how it speaks it. |
| FR-002 / Edge Cases | `OrderNumber` format `yyyyMMdd-##` | This is a **business identifier format**, not an implementation choice — the format is what end users see and reference in audits. |

### Validation result

**PASS** — proceed to `/speckit-clarify` (recommended, to close C-1 and C-2) before `/speckit-plan`. The two open clarifications are tracked here and in the spec itself with `[NEEDS CLARIFICATION: ...]` markers.

If you choose to skip `/speckit-clarify` and go straight to `/speckit-plan`, expect the plan phase to either inherit both ambiguities (and surface them in the implementation plan) or to force a decision on them.

Items marked incomplete with the actual checkbox `- [ ]` represent deliberate, documented exceptions, NOT spec defects. Do NOT silently flip them to passing.
