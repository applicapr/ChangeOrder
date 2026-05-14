# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]
**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

[Extract from feature spec: primary requirement + technical approach from research]

## Technical Context

<!--
  Project defaults populated from `.specify/memory/constitution.md` v1.0.0.
  Override per feature only if the constitution explicitly permits it; otherwise
  amend the constitution first.
-->

**Language/Version**: .NET 10 (LTS) + C# 14 — `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `GenerateDocumentationFile=true`  
**Primary Dependencies**: ASP.NET Core 10 Minimal APIs · EF Core 10 (Code-First) · Serilog · Asp.Versioning.Http · OpenAPI 3.1  
**Storage**: SQL Server (MSSQL); business identifiers (e.g., `OrderNumber`) enforced by UNIQUE constraint server-side  
**Testing**: xUnit + FluentAssertions + NSubstitute; naming `Method_Scenario_ExpectedResult`  
**Target Platform**: Docker on-premises (Linux); NOT exposed to the public internet  
**Project Type**: Onion-layered WebAPI — 5 projects (Domain, Business, Data, Presentation, Host)  
**Performance Goals**: 100 req/min rate limit per client (fixed window, HTTP 429 on excess); paginated lists with `PageSize ∈ [1..50]`  
**Constraints**: No `.cs` file > 500 lines; max 3 parameters per method/constructor (records exempt); no AutoMapper/Mapster; Result Pattern for domain errors  
**Scale/Scope**: Internal corporate API; estimated O(10k) orders/year initially

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Source: `.specify/memory/constitution.md` v1.0.0.

- [ ] **Gate-1 Onion (P-I, NON-NEGOTIABLE)**: New code respects inward-only ProjectReferences. `Domain` does NOT reference infrastructure (EF Core, MediatR, ASP.NET Core). `Presentation` does NOT reference `Data`. DI registrations live exclusively in `Host`.
- [ ] **Gate-2 Feature-Sliced CQRS (P-II)**: New business logic lives under `Business/Commands/<Feature>/` or `Business/Queries/<Feature>/`, one Command/Query + Validator + Handler per folder.
- [ ] **Gate-3 Result Pattern (P-III, NON-NEGOTIABLE)**: Business-flow failures return `Result<TValue, TError>`. Throwing on known business paths is rejected.
- [ ] **Gate-4 Persistent Auditability (P-IV)**: All new persisted entities implement `ISoftDeletable` + `IAuditable`. `AuditInterceptor` is the only writer of `CreatedAt`/`UpdatedAt`/`DeletedAt`/`IsDeleted`. New identifier columns carry UNIQUE constraints in the DB.
- [ ] **Gate-5 Composition + Manual Mapping (P-V, NON-NEGOTIABLE)**: New concrete classes are `sealed` by default. No abstract base class exists only to share helpers. All entity↔DTO mapping is manual static extension methods. AutoMapper/Mapster prohibited.
- [ ] **Gate-6 Quality bar**: No `.cs` file exceeds 500 lines. Every `await` in `Business`/`Data` uses `.ConfigureAwait(false)`. Every `async` method accepts and propagates `CancellationToken`. Every `catch` logs the full `ex` via `ILogger`/`Serilog.Log`.

Any unchecked gate MUST be justified in **Complexity Tracking** below; otherwise the plan is rejected.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)
<!--
  Project layout is fixed by `.specify/memory/constitution.md` v1.0.0 (Onion +
  CQRS, 5 csproj). Do NOT propose alternative layouts; amend the constitution
  first if a structural change is truly required.
-->

```text
src/
├── ChangeOrder.Domain/        # Entities, Value Objects, Enums, Errors, Abstractions — NO outward dependencies
├── ChangeOrder.Business/      # Commands/<Feature>/, Queries/<Feature>/, Services/, Extensions/
├── ChangeOrder.Data/          # Persistence/, Configurations/, Repositories/, Interceptors/, Migrations/, Extensions/
├── ChangeOrder.Presentation/  # Endpoints/, DTOs/{Requests,Responses}/, Mappers/, Extensions/
└── ChangeOrder.Host/          # Program.cs, appsettings.*.json, Dockerfile — Composition Root

tests/
├── ChangeOrder.Domain.Tests/
├── ChangeOrder.Business.Tests/
├── ChangeOrder.Data.Tests/
└── ChangeOrder.Presentation.Tests/
```

**Structure Decision**: Onion (5 `src/` projects + per-layer `tests/`). New feature work fits inside this layout — introducing a NEW top-level project requires a constitution amendment.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
