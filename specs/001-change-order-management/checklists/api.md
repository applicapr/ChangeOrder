# API Contract Quality Checklist: Change Order Management

**Purpose**: Validate that the API-related requirements (in `spec.md` and the contract in `contracts/openapi.yaml`) are complete, clear, consistent, and measurable. This is a "unit test for English" — it tests how the requirements are WRITTEN, not how the implementation behaves.
**Created**: 2026-05-12
**Feature**: [spec.md](../spec.md), [openapi.yaml](../contracts/openapi.yaml)

## Endpoint Coverage

- [ ] CHK001 Are every method × resource combination documented in both the spec and the OpenAPI contract? [Completeness, Spec §Requirements, openapi.yaml]
- [ ] CHK002 Is each workflow-advancement endpoint (`/{id}/approvals/{level}`, `/{id}/dates`) tied to at least one functional requirement in the spec? [Traceability, Spec §FR-3, FR-4]
- [ ] CHK003 Are the four approval levels in `/{id}/approvals/{level}` named consistently between spec (`Requester`, `Department Head`, `IT Head`, `Programming Division`) and OpenAPI enum (`requester`, `departmentHead`, `itHead`, `programmingDivision`)? [Consistency]
- [ ] CHK004 Does the spec say what the API should do when an approval level is set back to `Pending` after being `Approved`? [Gap, Spec §FR-4]

## Status Code Completeness

- [ ] CHK005 Does every endpoint declare all status codes it can return, including the rate-limit case? [Completeness, openapi.yaml]
- [ ] CHK006 Is the distinction between `409 Conflict` (state-illegal) and `422 Unprocessable` (payload-divergent idempotency) documented in the spec, not only in the contract? [Clarity, Spec §FR-6, FR-8, FR-12]
- [ ] CHK007 Are 5xx responses (e.g., 500 Internal Server Error, 503 Service Unavailable) part of any endpoint's documented contract, and if not is that omission intentional? [Coverage, Gap]
- [ ] CHK008 Is `204 No Content` distinguished from `200 OK` in the spec for `DELETE` and idempotent `POST` replays? [Clarity, Spec §FR-7, FR-8]

## Error Response Uniformity

- [ ] CHK009 Are the domain error codes (`order.not_found`, `order.duplicate_number`, `order.invalid_transition`, `order.edit_after_draft`, `order.daily_sequence_exhausted`, `idempotency.payload_divergence`) cataloged anywhere in the spec? [Gap]
- [ ] CHK010 Is the same RFC 7807 `ProblemDetails` shape (with the `code` extension) used for every error response across the API? [Consistency, openapi.yaml]
- [ ] CHK011 Are validation-failure response shapes specified for each `4xx` (specifically the `errors` field for per-property messages)? [Completeness, openapi.yaml]

## Schema Constraints

- [ ] CHK012 Are length bounds (`minLength`, `maxLength`) defined for every string property in `CreateOrderRequest` and `UpdateOrderRequest`? [Completeness, openapi.yaml]
- [ ] CHK013 Are required-vs-optional designations explicit for every property in every schema, with no implicit defaults? [Clarity, openapi.yaml]
- [ ] CHK014 Is the `OrderNumber` regex (`^\d{8}-\d{2}$`) consistent with the format described in the spec (`yyyyMMdd-##`)? [Consistency, Spec §FR-2]
- [ ] CHK015 Is the `pageSize` upper bound of 50 defined in both the spec and the OpenAPI contract? [Consistency, Spec §FR-5]

## Headers, Versioning, Idempotency

- [ ] CHK016 Is `Idempotency-Key` required (not optional) for `POST`, and are the format constraints (length, character set) explicit? [Clarity, openapi.yaml]
- [ ] CHK017 Is the API version (`/api/v1`) documented as required for every endpoint and is the policy for future versions (`v2`, deprecation timeline) declared? [Completeness, Gap]
- [ ] CHK018 Are `Retry-After` header semantics documented for the `429` response (units, value range)? [Clarity, openapi.yaml]
- [ ] CHK019 Are `Content-Type` and `Accept` negotiation requirements specified, or assumed to be `application/json` only? [Assumption, Gap]

## Examples & Discoverability

- [ ] CHK020 Does each endpoint specification include at least one request and one response example? [Completeness, Gap]
- [ ] CHK021 Is the `servers` block in the OpenAPI contract accurate for the actual local/internal deployments, with `internal.example` clearly marked as placeholder? [Clarity, openapi.yaml]

## Notes

- Each item targets the **written requirements**, not the running code.
- Mark `[x]` when the requirement quality issue is addressed in the spec or contract; mark with a comment + `[ ]` when the answer is "no, this is a real gap" so the reviewer leaves a paper trail.
- Items tagged `[Gap]` represent genuine missing requirements likely to surface during `/speckit-implement` or code review.
