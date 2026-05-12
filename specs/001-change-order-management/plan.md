# Implementation Plan: Change Order Management

**Branch**: `001-change-order-management` | **Date**: 2026-05-12 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-change-order-management/spec.md`

## Summary

Build a WebAPI on .NET 10 / ASP.NET Core 10 Minimal APIs that handles the full lifecycle of production change orders: capture, generate the thread-safe identifier `OrderNumber` of the form `yyyyMMdd-##`, drive the four-level approval chain, list/read/update/soft-delete orders, and expose the workflow-advancement endpoints that move an order from `Draft` to `Deployed`. Persistence is SQL Server with Entity Framework Core 10 Code-First, soft delete + audit trail via a `SaveChangesInterceptor`, and the database enforces the UNIQUE constraint on `OrderNumber` as the only safety net against concurrent collisions. Idempotency on `POST` is implemented with a dedicated `dbo.IdempotencyKeys` table with a 24h retention window. Authorization in Fase 1 is network-scoped (internal CORS, no public exposure); per-role authorization is deferred to Fase 2. The implementation follows the Onion layout fixed by the constitution: five projects (`Domain`, `Business`, `Data`, `Presentation`, `Host`) with strict inward-only references and feature-sliced CQRS inside `Business`.

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

- [x] **Gate-1 Onion (P-I, NON-NEGOTIABLE)**: Plan adopts the 5-project layout exactly as fixed by the constitution. `Domain` will have zero PackageReferences to infrastructure; `Presentation` references only `Business`; `Host` is the only Composition Root. Verified by Project Structure section below.
- [x] **Gate-2 Feature-Sliced CQRS (P-II)**: Plan places handlers under `Business/Commands/<Feature>/` (CreateOrder, UpdateOrder, DeleteOrder, RecordApproval, AdvanceWorkflow) and `Business/Queries/<Feature>/` (GetOrderById, GetAllOrders). One Command/Query + Validator + Handler per folder. See data-model.md and contracts/openapi.yaml.
- [x] **Gate-3 Result Pattern (P-III, NON-NEGOTIABLE)**: All business-flow failures use `Result<TValue, TError>` returning `DomainErrors.*` codes. Exception throws restricted to truly exceptional paths (I/O, network, bug). Plan documents the error catalog in research.md.
- [x] **Gate-4 Persistent Auditability (P-IV)**: `ChangeOrder` entity implements `ISoftDeletable` + `IAuditable`. `AuditInterceptor` is the only writer of audit/soft-delete columns. `OrderNumber` carries a UNIQUE constraint (`IX_ChangeOrders_OrderNumber`). All read paths honor the global `HasQueryFilter`. The `IdempotencyKeys` table is treated as ephemeral and is exempt from audit columns by design (research.md documents this).
- [x] **Gate-5 Composition + Manual Mapping (P-V, NON-NEGOTIABLE)**: Concrete classes (`ChangeOrderRepository`, `OrderNumberGenerator`, handlers) declared `sealed`. No abstract bases for helper-sharing. `OrderMapper` is a static extension class. Zero AutoMapper / Mapster references in the plan.
- [x] **Gate-6 Quality bar**: Each handler/repository/endpoint group designed to stay well under the 500-line ceiling. `.ConfigureAwait(false)` mandated on every `await` in `Business`/`Data`. Every `async` method accepts and propagates `CancellationToken`. Logging via `ILogger<T>` + Serilog with `ex` always captured in the catch.

All 6 gates pass at the design stage. **Re-evaluation required** after `/speckit-tasks` and during `/speckit-implement` — a gate is not "checked" forever; it is checked again whenever the code changes.

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
