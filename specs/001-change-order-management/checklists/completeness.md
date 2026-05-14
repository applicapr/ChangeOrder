# Requirements Completeness Checklist: Change Order Management

**Purpose**: Cross-cut over the spec to check that every requirement, user story, success criterion, and edge case is traceable, testable, and non-contradictory. Complements `requirements.md` (general spec quality) by focusing on traceability across artifacts.
**Created**: 2026-05-12
**Feature**: [spec.md](../spec.md)

## Functional Requirements ↔ User Stories

- [ ] CHK001 Does every Functional Requirement (FR-001..FR-012) map to at least one User Story (US1/US2/US3) that exercises it? [Traceability, Spec §FR + §User Stories]
- [ ] CHK002 Does every User Story have at least one Functional Requirement it relies on, with no orphan stories? [Traceability]
- [ ] CHK003 Is there any acceptance scenario in the spec that depends on a behavior NOT covered by any FR (silent requirement)? [Gap]

## Functional Requirements ↔ Success Criteria

- [ ] CHK004 Does every Success Criterion (SC-001..SC-007) trace to one or more Functional Requirements? [Traceability, Spec §FR + §SC]
- [ ] CHK005 Are there Functional Requirements whose correctness is NOT verifiable through any Success Criterion (FR exists but no SC tests it)? [Gap]
- [ ] CHK006 Is the relationship between FR-008 (idempotency on POST), FR-012 (idempotency storage) and SC-003 (idempotent retry returns same order) consistent across all three statements? [Consistency, Spec §FR-8, §FR-12, §SC-003]

## State Coverage

- [ ] CHK007 Does every `OrderStatus` value (`Draft`, `PendingApproval`, `Approved`, `InProgress`, `Deployed`, `Cancelled`) appear in at least one acceptance scenario, edge case or success criterion? [Coverage]
- [ ] CHK008 Is the `Cancelled` terminal state's entry path documented (who/when can cancel — any state? Only `PendingApproval` and `InProgress`?)? [Gap, Spec §FR-3]
- [ ] CHK009 Is the rejection-loop path ("approver marks `Rejected`, requester edits, re-submits for approval") described concretely in scenarios, not only mentioned in Edge Cases? [Coverage, Spec §Edge Cases, §Assumptions]

## Edge Cases ↔ Implementation Hooks

- [ ] CHK010 Is each item listed under "Edge Cases" tied to a corresponding behavior in the Functional Requirements (so that the implementation will not silently ignore it)? [Traceability]
- [ ] CHK011 Is the "Sequence exhaustion (>99/day)" edge case addressed by a specific FR (none today) or only by the `OrderNumber.Create` invariant? [Gap, Spec §Edge Cases]
- [ ] CHK012 Is "Concurrent modification (two operators editing the same order)" addressed by any FR? [Gap, Spec §Edge Cases]

## Assumptions Hygiene

- [ ] CHK013 Are all assumptions in the spec scoped explicitly to Fase 1 (with a tag or section heading)? [Clarity, Spec §Assumptions]
- [ ] CHK014 Does every "out of scope for Fase 1" assumption identify the Fase 2 owner / next step? [Traceability]
- [ ] CHK015 Is the implicit assumption "submission UTC timestamp = OrderNumber prefix" tested in an acceptance scenario? [Coverage, Spec §Edge Cases]

## Audit & Soft-Delete Coherence

- [ ] CHK016 Are AS-001..AS-004 traceable to a Functional Requirement that exercises them (e.g., AS-004 ↔ FR-007 DELETE)? [Traceability, Spec §AS-* + §FR-7]
- [ ] CHK017 Is the rule "soft-deleted records remain physically present and accessible to auditors" stated as both a requirement AND a success criterion (it currently appears as SC-004)? [Consistency, Spec §AS-004, §SC-004]

## Clarifications Closure

- [ ] CHK018 Are the resolutions of C-1 (FR-006 authorization scope) and C-2 (FR-012 idempotency storage) reflected in BOTH the body of the spec AND the `## Clarifications` section, with no contradictory wording remaining? [Consistency, Spec §Clarifications]
- [ ] CHK019 Are there ANY remaining `[NEEDS CLARIFICATION]` markers in the spec (target: 0)? [Completeness, Spec § entire document]

## Terminology

- [ ] CHK020 Is each domain term used consistently across spec, plan, research, data-model, and OpenAPI contract (e.g., `Requester` vs `Solicitante`, `Change Order` vs `Request`)? [Consistency]
- [ ] CHK021 Are abbreviations / acronyms (e.g., `VO`, `CQRS`, `AS-*`, `FR-*`, `SC-*`) defined where they first appear or in a glossary section? [Clarity, Gap]

## Notes

- This list tests cross-artifact coherence. Use it as the last pass before `/speckit-tasks` so that the implementation plan inherits a self-consistent spec.
- `[Gap]` markers here typically point at TODOs for either a follow-up `/speckit-clarify` round or for explicit "deferred to Fase 2" notes in the spec.
