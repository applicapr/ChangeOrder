<!--
Sync Impact Report
==================
Version change: (initial draft) → 1.0.0
Bump rationale: First ratified version of the project constitution; no prior
   semantic version exists, so this is treated as the MAJOR baseline.

Modified principles (compared to placeholder template):
   - I. Library-First         → I. Onion Architecture (NON-NEGOTIABLE)
   - II. CLI Interface        → II. Feature-Sliced CQRS
   - III. Test-First          → III. Result Pattern for Domain Errors (NON-NEGOTIABLE)
   - IV. Integration Testing  → IV. Persistent Auditability (Soft Delete + Audit)
   - V. Observability/...     → V. Composition over Inheritance, Manual Mapping (NON-NEGOTIABLE)

Added sections:
   - Technical Standards (Section 2)
   - Development Workflow & Quality Gates (Section 3)

Removed sections: None (placeholder template fully consumed).

Templates synced from this constitution (2026-05-12):
   ✅ updated — .specify/templates/plan-template.md
        Technical Context pre-populated with .NET 10 / C# 14 / SQL Server / EF Core 10
        as project defaults. Constitution Check expanded to 6 explicit gates
        (Onion, CQRS, Result Pattern, Auditability, Composition+Manual Mapping,
        Quality bar). Project Structure replaced with the 5-csproj Onion layout.
   ✅ updated — .specify/templates/spec-template.md
        Added mandatory section "Audit & Soft-Delete Impact" (AS-001..AS-004)
        after Functional Requirements; explicit "N/A" allowed when the feature
        is read-only / non-persistent.
   ✅ updated — .specify/templates/tasks-template.md
        Path Conventions rewritten with src/ChangeOrder.{Domain,Business,Data,
        Presentation,Host}/ and tests/ChangeOrder.*.Tests/, including the
        500-line rule and an explicit prohibition of src/models/, src/services/,
        and backend/ + frontend/ layouts.

Follow-up TODOs:
   - TODO(IDEMPOTENCY_STORAGE): Choose persistence for Idempotency-Key (table
        IdempotencyKeys vs IDistributedCache); document the decision under
        Technical Standards once made.
   - TODO(AUTHZ_MATRIX): Define authorization matrix per OrderStatus before
        implementing Update/Delete handlers.
-->

# ChangeOrder.Api Constitution

## Core Principles

### I. Onion Architecture (NON-NEGOTIABLE)

The solution MUST be split into five projects whose ProjectReferences point
inward only: `Domain` ← `{Business, Data}` ← `Presentation` ← `Host`.
`Domain` MUST NOT reference Entity Framework Core, MediatR, ASP.NET Core, or
any infrastructure package. `Presentation` MUST NOT reference `Data` —
persistence is accessed exclusively through `Business`. `Host` is the single
Composition Root; all dependency injection registrations MUST live there. No
transitive `ProjectReference` shortcuts are permitted. Rationale: keeps domain
logic portable and testable without infrastructure, and prevents the
architectural drift that allows controllers to query the database directly.

### II. Feature-Sliced CQRS

`Business` MUST organize code by feature folders containing a Command/Query +
Validator + Handler, never by technical layer (e.g., `Services/`, `Helpers/`).
Each handler addresses exactly one Command or Query. Handlers implement
`ICommandHandler<TCommand, TResult>` or `IQueryHandler<TQuery, TResult>`.
Cross-cutting helpers (e.g., `OrderNumberGenerator`) live under `Services/`.
Rationale: aligns code with the way features ship and are versioned, and
caps handler size at the unit of business intent.

### III. Result Pattern for Domain Errors (NON-NEGOTIABLE)

Business-flow failures (validation errors, not-found, conflict, rule
violations) MUST be returned through `Result<TValue, TError>`. Exceptions are
reserved for the unexpected — I/O failure, network failure, bug. A central
`DomainErrors` static catalog defines every reusable error code
(e.g., `DomainErrors.Order.NotFound(id)`, `DomainErrors.Order.DuplicateNumber`).
Throwing on a known business failure path is a defect. Rationale: makes
control flow explicit, avoids the cost and obscurity of exception-as-flow,
and produces uniform `ProblemDetails` responses.

### IV. Persistent Auditability (Soft Delete + Audit Trail)

Records MUST NOT be physically deleted. All persisted entities implement
`ISoftDeletable` (`IsDeleted`, `DeletedAt`) and `IAuditable` (`CreatedAt`,
`UpdatedAt`). An `AuditInterceptor` (`SaveChangesInterceptor`) is the single
mechanism that sets these fields automatically; handlers MUST NOT set them
manually. A global `HasQueryFilter` excludes soft-deleted records from every
read by default. The `OrderNumber` column MUST carry a UNIQUE index in the
database — the database is the source of truth for sequence collisions,
not application code. Rationale: compliance, traceability under audit, and a
single race-safe guarantee for the `yyyyMMdd-##` identifier.

### V. Composition over Inheritance & Manual Mapping (NON-NEGOTIABLE)

`sealed` is the default for concrete classes; inheritance is permitted only
for true is-a relationships (e.g., framework base types like `DbContext`).
Abstract base classes that exist solely to share helpers are forbidden —
extract a service or extension method instead. Object mapping between
entities and DTOs MUST be implemented as manual static extension methods
(`OrderMapper.ToResponse(entity)`). **AutoMapper, Mapster, or any reflection-
based mapper is prohibited.** Mappers MUST NOT contain business logic.
Rationale: explicit, debuggable, AOT-friendly code paths; eliminates a class
of bugs caused by silent property-name conventions and runtime profiles.

## Technical Standards

**Platform**: .NET 10 (LTS) + C# 14. `Nullable=enable`, `ImplicitUsings=enable`,
`TreatWarningsAsErrors=true`, `GenerateDocumentationFile=true` — set globally
via `Directory.Build.props`.

**Code style**:
- File-scoped namespaces, enforced as an `error` in `.editorconfig`.
- Explicit types; `var` only when the literal makes the type obvious.
- Primary constructors when applicable.
- Max 500 lines per `.cs` file (excluding auto-generated; blank lines and
  `///` comments do not count). CI MUST fail if violated.
- Max 3 parameters in constructors/methods (`record` types are exempt as
  data carriers).
- One top-level type per file; file name matches the type.
- `CancellationToken` MUST be a parameter on every `async` method and MUST
  be propagated through call chains.
- `.ConfigureAwait(false)` on every `await` in `Business` and `Data`; never
  in `Presentation` or `Host`.
- All string comparison APIs (`Equals`, `StartsWith`, `EndsWith`, `Contains`,
  `IndexOf`, `Replace`) MUST pass an explicit `StringComparison` —
  `Ordinal` for technical strings, `OrdinalIgnoreCase` for user-facing
  case-insensitive comparisons.

**API surface**:
- ASP.NET Core 10 Minimal APIs only (no Controllers).
- Endpoints registered as static classes with `IEndpointRouteBuilder` extension
  methods. Base URL versioned as `/api/v1/change-orders` via `Asp.Versioning.Http`.
- `TypedResults` only (no `Results`).
- Listing endpoints MUST paginate via `PagedRequest`/`PagedResponse<T>`,
  with `Page >= 1` and `PageSize ∈ [1..50]`.
- `POST` MUST honor the `Idempotency-Key` header.
- Rate limiting: built-in .NET 10, fixed window, 100 req/min/client; HTTP 429
  on excess.
- OpenAPI 3.1 document is the source of API truth; XML doc comments MUST be
  present on every public type and endpoint.

**Persistence**: SQL Server with EF Core 10 Code-First migrations. `OrderNumber`
column is `varchar(13)` with a UNIQUE index. Required indexes on `RequestDate`,
`Status`, and `IsDeleted`.

**Observability & Security**:
- Serilog structured logging to Console + rolling File sinks.
- Every `catch` MUST log the full `ex` to `ILogger` or `Serilog.Log`.
- Logs MUST NOT contain personally identifying data in plaintext.
- `/health` endpoint MUST verify SQL Server connectivity.
- CORS allow-list restricted to internal corporate hosts. The API is NOT
  exposed to the public internet.

**Testing stack**: xUnit + FluentAssertions + NSubstitute. Test naming:
`MethodUnderTest_Scenario_ExpectedResult`.

## Development Workflow & Quality Gates

**Branching**: `main` is protected. Work happens on `feature/<topic>`,
`feature/<user>/<topic>`, `fix/<topic>`, or `release/vX.Y.Z`. Direct commits
to `main` are forbidden. Merges into `main` require a PR with merge commit
(`--no-ff`).

**Commits**: Conventional Commits with project scope are mandatory:
`feat(domain):`, `fix(data):`, `chore(host):`, `docs(readme):`,
`refactor(business):`, `test(business):`, `docs(constitution):`, etc.

**Pre-commit checklist** (every commit, no exceptions):
1. `dotnet format` — fixes whitespace and style.
2. `dotnet build` — MUST pass with 0 warnings (warnings-as-errors is on).
3. `dotnet test` — all tests pass.
4. Live verification — when a feature is user-visible, validate it running
   before requesting review.
5. Verify `.csproj` references are immediate-only and respect the Onion graph.
6. Verify every `catch` logs via `ILogger`/`Serilog.Log`.
7. Verify project documentation (`README.md`, `LAYER.md` per project) is
   coherent with the change.

**CI/CD** (GitHub Actions, `.github/workflows/ci.yml`):
restore → build (0 warnings) → test → file-size check (no `.cs > 500` lines)
→ optional publish artifact. Triggers: push to `main`, PR to `main`.

**Deployment**: Multi-stage Docker image based on `mcr.microsoft.com/dotnet/sdk:10`
(build) and `mcr.microsoft.com/dotnet/aspnet:10` (runtime). Deployed only to
internal corporate infrastructure; never exposed to the public internet.

**Repository policy on AI artifacts**: Per-developer AI tooling artifacts
(agent memory, agent instructions, locally generated slash commands) MUST be
listed in `.gitignore` and MUST NOT be committed. Exception: artifacts of the
GitHub Spec Kit workflow (`.specify/`, `Docs/0-Initial/`) ARE product
documentation and ARE committed.

## Governance

This constitution supersedes any informal practice, README guidance, or
team folklore that contradicts it. Conflicts MUST be resolved by amending
this document, not by silently deviating.

**Amendment procedure**:
1. Open a PR with scope `docs(constitution):` containing the proposed change.
2. Justify the change against existing principles in the PR description.
3. Bump `Version` per semantic versioning (see below) and update
   `Last Amended`.
4. Propagate the change to `.specify/templates/` (plan, spec, tasks) where
   relevant, and to runtime guidance (`README.md`, `Docs/`, per-project
   `LAYER.md`).
5. Merge requires explicit approval from a code owner; the merge commit MUST
   reference the new version in its body.

**Versioning policy** (semantic versioning for the constitution itself):
- **MAJOR**: A principle is removed, redefined in a backward-incompatible
   way, or a governance rule is overturned.
- **MINOR**: A new principle or a materially new normative section is added.
- **PATCH**: Clarifications, wording, typo fixes, or non-semantic refinements.

**Compliance review**: Every PR review MUST verify compliance with the
applicable principles. The PR template (when introduced) will include a
checklist mirroring the five Core Principles. Reviewers MUST cite the
specific principle when requesting changes on architectural grounds.

**Runtime development guidance**: Day-to-day execution guidance (commands,
hot tips, environment workarounds) lives in `CLAUDE.md` (gitignored,
per-developer) and `README.md` (committed); neither overrides this
constitution.

**Version**: 1.0.0 | **Ratified**: 2026-05-12 | **Last Amended**: 2026-05-12
