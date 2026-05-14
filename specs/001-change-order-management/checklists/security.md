# Security Requirements Quality Checklist: Change Order Management

**Purpose**: Validate the quality of security-related requirements in `spec.md`. Because Fase 1 deliberately defers identity-aware authorization to Fase 2, several items here will surface as legitimate `[Gap]`s — that is informative, not a failure of the spec.
**Created**: 2026-05-12
**Feature**: [spec.md](../spec.md)

## Authentication Scope

- [ ] CHK001 Is the Fase 1 authentication model ("network isolation + internal CORS, no public exposure") stated unambiguously somewhere readers cannot miss? [Clarity, Spec §Assumptions]
- [ ] CHK002 Does the spec name the trust boundary (corporate intranet, VPN, etc.) and explicitly exclude internet exposure? [Clarity, Spec §Assumptions]
- [ ] CHK003 Is the absence of identity-aware authentication in Fase 1 acknowledged as a known accepted risk rather than an oversight? [Assumption, Spec §Assumptions]

## Authorization

- [ ] CHK004 Is FR-006's "PUT only allowed in Draft" rule consistent with US3 Scenario 3 ("rejects modification with explanation grounded in current state")? [Consistency, Spec §FR-6 + §US3]
- [ ] CHK005 Does the spec say WHO can transition an order — anyone with network access, or some not-yet-implemented role? [Gap, Spec §FR-3]
- [ ] CHK006 Are the workflow-advancement endpoints (`/{id}/approvals/{level}`, `/{id}/dates`) implicitly trusting "any caller is an authorized approver" in Fase 1, and is that explicit? [Assumption, openapi.yaml]
- [ ] CHK007 Is the Fase 2 authorization-matrix dependency (FR-006 deferral) tracked somewhere so it does not get lost between phases? [Traceability, Spec §Clarifications, §Assumptions]

## Sensitive Data & Logging

- [ ] CHK008 Is "no PII in plaintext logs" stated as a measurable / verifiable requirement, not a goal? [Clarity, Spec §FR-11]
- [ ] CHK009 Are the fields considered "sensitive" enumerated (e.g., `Requester.Email`, free-text justification with potential PII), so reviewers can audit log statements? [Gap, Spec §FR-11]
- [ ] CHK010 Is the retention period for application logs documented? [Gap]

## Input Validation

- [ ] CHK011 Are length bounds on free-text fields (`Justification`, `RequestDetails`, `WorkDescription`) stated explicitly to bound payload size and reduce DoS surface? [Coverage, data-model.md §1]
- [ ] CHK012 Are file-path fields (`VersionScreenshotPath`, `PostDeployScreenshotPath`) validated against path traversal / arbitrary file references? [Gap]
- [ ] CHK013 Is the boundary between "the API stores a path string" and "the API actually serves/uploads the file" stated, to avoid implicit file-server obligations? [Ambiguity, data-model.md §1]

## Rate Limiting

- [ ] CHK014 Is the rate-limit partition key (`per-IP` in Fase 1, `per-authenticated-principal` in Fase 2) documented? [Clarity, research.md R-7]
- [ ] CHK015 Is the rate-limit behavior under shared NAT / shared corporate proxy addressed (every internal user appearing as one IP)? [Gap]
- [ ] CHK016 Are the privileged paths (`/health`, OpenAPI doc) excluded from the rate limit, and is that documented? [Gap]

## Data Retention

- [ ] CHK017 Is the 24h `Idempotency-Key` retention documented as both a requirement and a runtime obligation (cleanup job)? [Consistency, Spec §FR-12, research.md R-2]
- [ ] CHK018 Is the retention policy for soft-deleted orders documented (kept indefinitely vs purged after N years for compliance)? [Gap, Spec §AS-004]

## Threat Model & Compliance

- [ ] CHK019 Is there any explicit threat model (even informal: "untrusted internal users vs trusted ops") in the spec or research? [Gap]
- [ ] CHK020 Are compliance / audit obligations (e.g., SOX, internal audit cadence) tied to the soft-delete + audit-trail requirement, so the rationale is clear when the rule is questioned? [Traceability, Spec §AS-001..AS-004]

## Notes

- The Fase 1 security posture is **deliberately thin** because the system runs inside a closed corporate network. The checklist marks `[Gap]` where the requirements are silent; many of those gaps are acceptable for Fase 1 but should be revisited at the Fase 2 transition.
