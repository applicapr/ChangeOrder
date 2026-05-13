---

description: "Implementation tasks for Change Order Management"
---

# Tasks: Change Order Management

**Input**: Design documents from `/specs/001-change-order-management/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/openapi.yaml, quickstart.md

**Tests**: INCLUDED. The spec mandates them via SC-001 (concurrency test for OrderNumber) and SC-006 (illegal transition tests). The constitution requires per-layer test projects with xUnit + FluentAssertions + NSubstitute.

**Organization**: Tasks are grouped by user story. Phase 1 (Setup) and Phase 2 (Foundational) are prerequisites for all stories; once they complete, the three user-story phases can in principle run in parallel by different developers.

## Format: `[ID] [P?] [Story] Description`

- `[P]` — task is parallelizable (different files, no dependencies on incomplete tasks in the same phase)
- `[Story]` — US1, US2, US3 (omitted in Setup, Foundational and Polish phases)

## Path Conventions

Onion layout fixed by `.specify/memory/constitution.md` v1.0.0:

- **Production code**: `src/ChangeOrder.{Domain,Business,Data,Presentation,Host}/`
- **Tests**: `tests/ChangeOrder.{Domain,Business,Data,Presentation}.Tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Bring the empty repository to the point where `dotnet build` produces five empty assemblies that respect the Onion graph.

- [X] T001 Create `Directory.Build.props` at the repo root with `TargetFramework=net10.0`, `LangVersion=14`, `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `GenerateDocumentationFile=true`
- [X] T002 [P] Create `.editorconfig` at the repo root enforcing file-scoped namespaces as `error`, explicit types, modifiers always visible, max line length advisory
- [X] T003 [P] Create the empty solution file `ChangeOrder.slnx` referencing the five projects under `src/`
- [X] T004 Create `src/ChangeOrder.Domain/ChangeOrder.Domain.csproj` (`Microsoft.NET.Sdk`, no PackageReferences, no ProjectReferences)
- [X] T005 Create `src/ChangeOrder.Business/ChangeOrder.Business.csproj` (`Microsoft.NET.Sdk`, ProjectReference to `Domain` only)
- [X] T006 Create `src/ChangeOrder.Data/ChangeOrder.Data.csproj` (`Microsoft.NET.Sdk`, ProjectReference to `Domain`, PackageReferences for `Microsoft.EntityFrameworkCore.SqlServer` v10.0.x and `Microsoft.EntityFrameworkCore.Design` v10.0.x)
- [X] T007 Create `src/ChangeOrder.Presentation/ChangeOrder.Presentation.csproj` (`Microsoft.NET.Sdk`, ProjectReference to `Business` only, PackageReferences for `Asp.Versioning.Http` and `Microsoft.AspNetCore.OpenApi`)
- [X] T008 Create `src/ChangeOrder.Host/ChangeOrder.Host.csproj` (`Microsoft.NET.Sdk.Web`, ProjectReferences to `Presentation` and `Data`, PackageReferences for Serilog stack + `AspNetCore.HealthChecks.SqlServer`, AND the `<InterceptorsNamespaces>$(InterceptorsNamespaces);Microsoft.AspNetCore.OpenApi.Generated</InterceptorsNamespaces>` line per research.md R-8)
- [X] T009 Add the five test project skeletons: `tests/ChangeOrder.Domain.Tests/ChangeOrder.Domain.Tests.csproj`, `Business.Tests`, `Data.Tests`, `Presentation.Tests` (each `Microsoft.NET.Sdk`, ProjectReference to the corresponding production project, PackageReferences for `xunit`, `FluentAssertions`, `NSubstitute`, `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`)
- [X] T010 Append the test projects to `ChangeOrder.slnx`
- [X] T011 Add `.github/workflows/ci.yml` with `restore → build (0 warnings) → test → 500-line-check` running on push to main and on PRs
- [X] T012 Add `src/ChangeOrder.Host/Dockerfile` multi-stage based on `mcr.microsoft.com/dotnet/sdk:10` (build) and `mcr.microsoft.com/dotnet/aspnet:10` (runtime)
- [X] T013 Run `dotnet build` end-to-end to confirm the empty solution compiles with **0 warnings, 0 errors** before moving on

**Checkpoint**: Empty Onion solution builds clean. No business code yet.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Code that every user story depends on. NO user-story work can begin until this phase completes.

### Domain plumbing (shared by all stories)

- [X] T014 [P] Add `src/ChangeOrder.Domain/Errors/Error.cs` — sealed record `Error(string Code, string Message)`
- [X] T015 [P] Add `src/ChangeOrder.Domain/Errors/Result.cs` — sealed generic record `Result<TValue, TError>` with `Success`/`Failure` factories per research.md R-5
- [X] T016 Add `src/ChangeOrder.Domain/Errors/DomainErrors.cs` — static class with nested `Order` and `Idempotency` classes producing every error code referenced in `openapi.yaml` (`order.not_found`, `order.duplicate_number`, `order.invalid_transition`, `order.edit_after_draft`, `order.daily_sequence_exhausted`, `order.concurrency_conflict`, `idempotency.payload_divergence`)
- [X] T017 [P] Add `src/ChangeOrder.Domain/Enums/OrderStatus.cs` — values: `Draft`, `PendingApproval`, `Approved`, `InProgress`, `Deployed`, `Cancelled`
- [X] T018 [P] Add `src/ChangeOrder.Domain/Enums/ApprovalStatus.cs` — values: `Pending`, `Approved`, `Rejected`
- [X] T019 [P] Add `src/ChangeOrder.Domain/Abstractions/ISoftDeletable.cs` — `bool IsDeleted` + `DateTime? DeletedAt`
- [X] T020 [P] Add `src/ChangeOrder.Domain/Abstractions/IAuditable.cs` — `DateTime CreatedAt` + `DateTime? UpdatedAt`
- [X] T021 [P] Add `src/ChangeOrder.Domain/Abstractions/IUnitOfWork.cs` — `Task<int> SaveChangesAsync(CancellationToken)`
- [X] T022 [P] Add `src/ChangeOrder.Domain/ValueObjects/OrderNumber.cs` — sealed record, private ctor, `Create(DateOnly date, int sequence)` factory returning `Result<OrderNumber, Error>`, format `yyyyMMdd-##`, validates `sequence ∈ [1..99]`
- [X] T023 [P] Add `src/ChangeOrder.Domain/ValueObjects/RequesterInfo.cs` — sealed record with `Name`, `Position`, `Department`, `Email`
- [X] T024 [P] Add `src/ChangeOrder.Domain/ValueObjects/ApprovalChain.cs` — sealed record with the four approval slots, helper `AllApproved()`
- [X] T025 Add `src/ChangeOrder.Domain/Entities/ChangeOrder.cs` — aggregate root implementing `ISoftDeletable` + `IAuditable`, encapsulating OrderNumber + Requester + ApprovalChain + Status + milestone dates, exposing a `RowVersion` `byte[]` property for optimistic concurrency (FR-013), with state-transition method(s) returning `Result<TVoid, Error>` per data-model.md §8
- [X] T026 [P] Add `src/ChangeOrder.Domain/Entities/IdempotencyKey.cs` — entity with `Key`, `OrderId`, `RequestHash`, `CreatedAt` (NOT auditable, NOT soft-deletable; documented in data-model.md §6)
- [X] T027 Add `src/ChangeOrder.Domain/Abstractions/IChangeOrderRepository.cs` — methods: `GetByIdAsync`, `ListAsync(PagedRequest)`, `AddAsync`, `GetNextSequenceForDateAsync(DateOnly)`, `FindIdempotencyAsync(string key)`
- [X] T028 [P] Add `src/ChangeOrder.Domain/Extensions/ServiceCollectionExtensions.cs` — `AddDomain(this IServiceCollection)` stub (Domain has no DI today, kept for symmetry)

### Data plumbing

- [X] T029 Add `src/ChangeOrder.Data/Persistence/ApplicationDbContext.cs` — `DbSet<ChangeOrder>`, `DbSet<IdempotencyKey>`, configured with the global query filter for soft delete
- [X] T030 [P] Add `src/ChangeOrder.Data/Configurations/ChangeOrderConfiguration.cs` — table `dbo.ChangeOrders`, all columns per data-model.md §1, UNIQUE INDEX `IX_ChangeOrders_OrderNumber`, non-clustered indexes on `RequestDate`, `Status`, `IsDeleted`, OwnsOne mappings for the three value objects, `Property(e => e.RowVersion).IsRowVersion()` for FR-013 concurrency token
- [X] T031 [P] Add `src/ChangeOrder.Data/Configurations/IdempotencyKeyConfiguration.cs` — table `dbo.IdempotencyKeys`, PK on `Key`, FK to `ChangeOrders.Id` with `Restrict` delete behavior, index on `CreatedAt`
- [X] T032 Add `src/ChangeOrder.Data/Interceptors/AuditInterceptor.cs` — implements `ISaveChangesInterceptor` per research.md R-4, single writer of audit/soft-delete columns
- [X] T033 Add `src/ChangeOrder.Data/Repositories/ChangeOrderRepository.cs` — sealed class implementing `IChangeOrderRepository`. The `GetNextSequenceForDateAsync` method uses the `UPDLOCK + HOLDLOCK` raw-SQL strategy from research.md R-1; ALL `await`s use `.ConfigureAwait(false)`
- [X] T034 Add `src/ChangeOrder.Data/Repositories/UnitOfWork.cs` — sealed wrapper for `ApplicationDbContext.SaveChangesAsync`
- [X] T035 Add `src/ChangeOrder.Data/Extensions/ServiceCollectionExtensions.cs` — `AddDataLayer(this IServiceCollection, IConfiguration)` registers `DbContext` (with `AuditInterceptor`), repositories and `IUnitOfWork`
- [X] T036 Add the EF Core migration `InitialCreate` via `dotnet ef migrations add InitialCreate --project src/ChangeOrder.Data --startup-project src/ChangeOrder.Host`; verify it creates both tables, the unique index, the three non-clustered indexes and the FK with the correct names

### Business plumbing

- [X] T037 [P] Add `src/ChangeOrder.Business/Abstractions/ICommandHandler.cs` — `ICommandHandler<TCommand, TResult>` with `Task<TResult> HandleAsync(TCommand, CancellationToken)`
- [X] T038 [P] Add `src/ChangeOrder.Business/Abstractions/IQueryHandler.cs` — `IQueryHandler<TQuery, TResult>` with same shape
- [X] T039 [P] Add `src/ChangeOrder.Business/Common/PagedRequest.cs` and `Common/PagedResponse.cs` — records used by listing handlers
- [X] T040 Add `src/ChangeOrder.Business/Extensions/ServiceCollectionExtensions.cs` — `AddBusinessLayer(this IServiceCollection)` discovers and registers every `ICommandHandler<,>` / `IQueryHandler<,>` in the assembly

### Presentation plumbing

- [X] T041 [P] Add `src/ChangeOrder.Presentation/Common/ProblemDetailsFactory.cs` — translates `Error` codes to RFC 7807 `ProblemDetails` payloads (one place to evolve the mapping)
- [X] T042 Add `src/ChangeOrder.Presentation/Extensions/ServiceCollectionExtensions.cs` — `AddPresentationLayer(this IServiceCollection)` registers API versioning, rate limiting (fixed window 100/min per IP per research.md R-7), OpenAPI generator
- [X] T043 Add `src/ChangeOrder.Presentation/Extensions/EndpointRouteBuilderExtensions.cs` — `MapChangeOrderApi(this IEndpointRouteBuilder)` defines the `/api/v1/change-orders` group with versioning and rate-limit policy (endpoint method bodies stubbed; implementation per user-story phase)

### Host wiring

- [X] T044 Add `src/ChangeOrder.Host/Program.cs` — `WebApplication.CreateBuilder`, Serilog bootstrap (console + rolling file), `AddDomain → AddDataLayer → AddBusinessLayer → AddPresentationLayer → AddOpenApi → AddHealthChecks (SQL Server)`; mapping: `MapOpenApi` (Development only), `UseHttpsRedirection`, `UseRateLimiter`, `MapHealthChecks("/health")`, `MapChangeOrderApi`
- [X] T045 [P] Add `src/ChangeOrder.Host/appsettings.json` and `appsettings.Development.json` with the `DefaultConnection` connection string placeholder and Serilog config per quickstart.md
- [X] T046 Verify the application starts (`dotnet run --project src/ChangeOrder.Host`), `/health` returns 200 Healthy when SQL Server is up, and `/openapi/v1.json` is served in Development

### Foundational tests

- [X] T047 [P] Add `tests/ChangeOrder.Domain.Tests/ValueObjects/OrderNumberTests.cs` — `Create_WithValidSequence_ReturnsOrderNumber`, `Create_WithSequenceZero_FailsWithDailySequenceExhausted`, `Create_WithSequence100_FailsWithDailySequenceExhausted`, `Create_FormatsAsExpected`
- [X] T048 [P] Add `tests/ChangeOrder.Domain.Tests/Errors/DomainErrorsTests.cs` — sanity tests asserting every error factory returns the expected `Code`
- [X] T049 [P] Add `tests/ChangeOrder.Data.Tests/Interceptors/AuditInterceptorTests.cs` — uses EF Core In-Memory provider where possible; verifies `CreatedAt`/`UpdatedAt`/`DeletedAt`/`IsDeleted` are set automatically

**Checkpoint**: Foundation ready. The application boots, the database accepts `InitialCreate`, the test suite is green though sparse.

---

## Phase 3: User Story 1 — Capture a change request and assign a unique order number (Priority: P1) 🎯 MVP

**Goal**: Realize the minimum-viable path: a complete `POST /api/v1/change-orders` that persists a new `ChangeOrder`, assigns a thread-safe `OrderNumber`, and honors `Idempotency-Key`.

**Independent Test**: Sending a complete `CreateOrderRequest` returns HTTP 201 with `orderNumber` matching `^\d{8}-\d{2}$` and `status="Draft"`. Sending the same `Idempotency-Key` twice produces a single row.

### Implementation for User Story 1

- [X] T050 [P] [US1] Add `src/ChangeOrder.Business/Services/OrderNumberGenerator.cs` — sealed class implementing R-1 strategy: delegates `GetNextSequenceForDateAsync` to the repository, retries up to 3 times on UNIQUE violation, returns `Result<OrderNumber, Error>`
- [X] T051 [P] [US1] Add `src/ChangeOrder.Business/Services/IdempotencyService.cs` — sealed class that computes SHA-256 of the canonicalized request body, looks up the key, returns `Existing`/`Conflict`/`Fresh` outcome per research.md R-2
- [X] T052 [P] [US1] Add `src/ChangeOrder.Business/Commands/CreateOrder/CreateOrderCommand.cs` — record carrying all `CreateOrderRequest` fields plus `IdempotencyKey`
- [X] T053 [US1] Add `src/ChangeOrder.Business/Commands/CreateOrder/CreateOrderValidator.cs` — `.NET 10 AddValidation()` rules for required text fields, length bounds, email shape (per data-model.md §3)
- [X] T054 [US1] Add `src/ChangeOrder.Business/Commands/CreateOrder/CreateOrderHandler.cs` — sealed `ICommandHandler<CreateOrderCommand, Result<OrderResponse, Error>>`. Flow: 1) IdempotencyService lookup; 2) on Fresh, ask OrderNumberGenerator for the next sequence; 3) build `ChangeOrder` entity; 4) persist within a single transaction (order + idempotency key row); 5) on UNIQUE violation, retry. `.ConfigureAwait(false)` everywhere.
- [X] T055 [P] [US1] Add `src/ChangeOrder.Presentation/DTOs/Requests/CreateOrderRequest.cs` — record matching `openapi.yaml` schema
- [X] T056 [P] [US1] Add `src/ChangeOrder.Presentation/DTOs/Responses/OrderResponse.cs` — record matching `openapi.yaml` schema, includes `RowVersion` (base64-encoded byte[]) for FR-013
- [X] T057 [P] [US1] Add `src/ChangeOrder.Presentation/Mappers/OrderMapper.cs` — static extension class with `ToResponse(this ChangeOrder)` and `ToEntity(this CreateOrderRequest, OrderNumber)`
- [X] T058 [US1] In `src/ChangeOrder.Presentation/Extensions/EndpointRouteBuilderExtensions.cs`, implement the `POST /api/v1/change-orders` endpoint: reads `Idempotency-Key` header, invokes the command handler, returns `TypedResults.Created(...)` on 201, `TypedResults.Ok(...)` on idempotent replay, `TypedResults.UnprocessableEntity(...)` on payload divergence, validation errors via `ProblemDetailsFactory`

### Tests for User Story 1

- [X] T059 [P] [US1] `tests/ChangeOrder.Business.Tests/Commands/CreateOrder/CreateOrderHandlerTests.cs` — happy path, validation error path, idempotent replay path (same key + same payload → returns existing), payload divergence path (same key + different payload → error)
- [X] T060 [P] [US1] `tests/ChangeOrder.Business.Tests/Services/OrderNumberGeneratorTests.cs` — generates `yyyyMMdd-01` for first call of the day; retries on `DbUpdateException` with a UNIQUE violation; gives up after 3 attempts with `DomainErrors.Order.DailySequenceExhausted`
- [~] T061 [US1] `tests/ChangeOrder.Data.Tests/Concurrency/OrderNumberConcurrencyTests.cs` — **Testcontainers SQL Server** test exercising 100 simultaneous `CreateOrderHandler.HandleAsync` calls; asserts 100 distinct `OrderNumber`s and zero failures (SC-001). Tagged `[Trait("Category", "Testcontainers")]`. Reason `[~]`: Docker daemon not running in dev environment; test compiles and gracefully returns when Docker is unavailable (see `_dockerAvailable` short-circuit). Will fully execute on CI / when Docker is up.
- [X] T062 [P] [US1] `tests/ChangeOrder.Presentation.Tests/Endpoints/CreateOrderEndpointTests.cs` — `WebApplicationFactory<Program>` end-to-end: 201 on success, 200 on idempotent replay, 422 on payload divergence, 400 on validation failure

**Checkpoint**: US1 fully functional and independently testable. Demoable.

---

## Phase 4: User Story 2 — Move the request through the four-level approval chain (Priority: P2)

**Goal**: Implement the approval workflow: `PUT /{id}/approvals/{level}` and the consequent `OrderStatus` transitions, plus the milestone-date endpoint that drives `Approved → InProgress → Deployed`.

**Independent Test**: Starting from an order created via US1, sequentially marking the four approvals as `Approved` moves the order through `Draft → PendingApproval → Approved`. Recording delivery and deploy dates moves it to `InProgress` and then `Deployed`.

### Implementation for User Story 2

- [ ] T063 [P] [US2] Add `src/ChangeOrder.Business/Commands/RecordApproval/RecordApprovalCommand.cs` — record with `OrderId`, `Level` (enum: `Requester`/`DepartmentHead`/`ItHead`/`ProgrammingDivision`), `Verdict`
- [ ] T064 [US2] Add `src/ChangeOrder.Business/Commands/RecordApproval/RecordApprovalHandler.cs` — loads order, applies verdict to the right slot, evaluates whether all four are `Approved` (transition `PendingApproval → Approved`); returns `Result<TVoid, Error>` with `InvalidStateTransition` if the order is not in an approval-accepting state
- [ ] T065 [P] [US2] Add `src/ChangeOrder.Business/Commands/RecordMilestoneDates/RecordMilestoneDatesCommand.cs` — nullable `DeliveryDate`, `InitialEvaluationDate`, `ProductionDeployDate`, `PostDeployScreenshotPath`
- [ ] T066 [US2] Add `src/ChangeOrder.Business/Commands/RecordMilestoneDates/RecordMilestoneDatesHandler.cs` — applies provided dates, drives transitions: setting `DeliveryDate` while `Approved` → `InProgress`; setting `ProductionDeployDate` while `InProgress` → `Deployed`; rejects out-of-order updates with `InvalidStateTransition`
- [ ] T067 [P] [US2] Add `src/ChangeOrder.Presentation/DTOs/Requests/ApprovalVerdictRequest.cs` and `MilestoneDatesRequest.cs` matching the OpenAPI schemas
- [ ] T068 [US2] Extend `EndpointRouteBuilderExtensions.MapChangeOrderApi` with `PUT /{id}/approvals/{level}` and `PATCH /{id}/dates` per `openapi.yaml`; both return 204 on success, 404 if not found, 409 on illegal transition

### Tests for User Story 2

- [ ] T069 [P] [US2] `tests/ChangeOrder.Business.Tests/Commands/RecordApproval/RecordApprovalHandlerTests.cs` — every combination of `(currentChain, level, verdict)` exercising the state machine; covers SC-006 (illegal-transition tests)
- [ ] T070 [P] [US2] `tests/ChangeOrder.Business.Tests/Commands/RecordMilestoneDates/RecordMilestoneDatesHandlerTests.cs` — Approved → InProgress on delivery date; InProgress → Deployed on production deploy date; rejects setting `ProductionDeployDate` while still `Approved`
- [ ] T071 [P] [US2] `tests/ChangeOrder.Presentation.Tests/Endpoints/WorkflowEndpointsTests.cs` — end-to-end: full approval chain to Deployed, plus rejection variants returning 409

**Checkpoint**: US1 + US2 working independently and together. A submitter can create + approve + deploy an order end to end.

---

## Phase 5: User Story 3 — List, search and maintain existing orders (Priority: P3)

**Goal**: Implement listing (paginated), single-order lookup, the constrained `PUT` (Draft only per FR-006 and the C-1 clarification), and the soft `DELETE`.

**Independent Test**: With several orders in the database (some via US1, some soft-deleted), `GET /api/v1/change-orders?page=1&pageSize=10` returns the right page, soft-deleted excluded; `GET /{id}` returns 200/404 correctly; `PUT /{id}` succeeds in Draft and returns 409 once the order is in `PendingApproval`; `DELETE /{id}` flips `IsDeleted=true` without removing the row.

### Implementation for User Story 3

- [ ] T072 [P] [US3] Add `src/ChangeOrder.Business/Queries/GetOrderById/GetOrderByIdQuery.cs` (record with `Id`)
- [ ] T073 [P] [US3] Add `src/ChangeOrder.Business/Queries/GetOrderById/GetOrderByIdHandler.cs` — returns `Result<OrderResponse, Error>`; 404 emits `DomainErrors.Order.NotFound`
- [ ] T074 [P] [US3] Add `src/ChangeOrder.Business/Queries/GetAllOrders/GetAllOrdersQuery.cs` (extends `PagedRequest`)
- [ ] T075 [US3] Add `src/ChangeOrder.Business/Queries/GetAllOrders/GetAllOrdersHandler.cs` — returns `PagedResponse<OrderResponse>`; validates `Page>=1`, `PageSize ∈ [1..50]`, exposes `TotalCount` from `CountAsync`
- [ ] T076 [P] [US3] Add `src/ChangeOrder.Business/Commands/UpdateOrder/UpdateOrderCommand.cs` — includes `RowVersion` (byte[]) for FR-013
- [ ] T077 [US3] Add `src/ChangeOrder.Business/Commands/UpdateOrder/UpdateOrderHandler.cs` — only accepts mutation when `OrderStatus == Draft`; otherwise emits `DomainErrors.Order.EditAfterDraft` (HTTP 409) per the C-1 clarification. Sets the entity's `RowVersion` from the command before `SaveChangesAsync`; catches `DbUpdateConcurrencyException` and returns `DomainErrors.Order.ConcurrencyConflict` (HTTP 409) per FR-013
- [ ] T078 [P] [US3] Add `src/ChangeOrder.Business/Commands/DeleteOrder/DeleteOrderCommand.cs`
- [ ] T079 [US3] Add `src/ChangeOrder.Business/Commands/DeleteOrder/DeleteOrderHandler.cs` — calls `Remove` on the entity; the `AuditInterceptor` converts this to a soft delete automatically (no manual flag flipping)
- [ ] T080 [P] [US3] Add `src/ChangeOrder.Presentation/DTOs/Requests/UpdateOrderRequest.cs` — extends `CreateOrderRequest` shape and includes a required `RowVersion` (base64-encoded) for FR-013
- [ ] T081 [US3] Extend `EndpointRouteBuilderExtensions.MapChangeOrderApi` with `GET /` (paged), `GET /{id}`, `PUT /{id}` (Draft-only), `DELETE /{id}`, all wired to their respective handlers and `ProblemDetailsFactory`

### Tests for User Story 3

- [ ] T082 [P] [US3] `tests/ChangeOrder.Business.Tests/Queries/GetAllOrdersHandlerTests.cs` — pagination math (`TotalPages`, edge cases at `Page=1` and `Page=lastPage`), invalid pageSize rejected
- [ ] T083 [P] [US3] `tests/ChangeOrder.Business.Tests/Commands/UpdateOrderHandlerTests.cs` — succeeds in Draft, returns `EditAfterDraft` in every other status
- [ ] T084 [P] [US3] `tests/ChangeOrder.Data.Tests/SoftDelete/SoftDeleteQueryFilterTests.cs` — a soft-deleted order is invisible to `GET` paths; using `IgnoreQueryFilters()` makes it visible (audit-tool surface, future use)
- [ ] T085 [P] [US3] `tests/ChangeOrder.Presentation.Tests/Endpoints/MaintenanceEndpointsTests.cs` — list/get/update/delete end-to-end against `WebApplicationFactory<Program>`

**Checkpoint**: All three user stories pass independently. The full CRUD + governance surface is functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Wire the production-only concerns and harden the build.

- [ ] T086 [P] Add `src/ChangeOrder.Host/BackgroundServices/IdempotencyCleanupService.cs` — `BackgroundService` running every hour, deleting `IdempotencyKeys` rows older than 24h (per research.md R-2)
- [ ] T087 [P] Add Serilog enrichers (correlation id, environment) and the production sink configuration in `appsettings.Production.json`
- [ ] T088 [P] Add the rate-limit policy customization (1-minute fixed window, 100 permits per IP) registered in `AddPresentationLayer` per research.md R-7; `Retry-After` header emitted on rejection
- [X] T088a [P] Add `GET /version` operational endpoint (global, outside the `/api/v{version}` group). Returns `{ name, version, environment }` sourced from `Directory.Build.props` `<Version>` via `AssemblyInformationalVersionAttribute`. Mapped from `EndpointRouteBuilderExtensions.MapVersionEndpoint`, tag `meta`, schema in `contracts/openapi.yaml`. Test: `VersionEndpointTests.Get_Version_Returns200WithIdentityPayload`
- [ ] T089 [P] Add XML doc comments on every public endpoint and DTO so the generated OpenAPI matches `contracts/openapi.yaml`
- [ ] T090 [P] Verify (via a CI step or pre-commit script) that no `.cs` file exceeds 500 lines (constitution Quality bar gate)
- [ ] T091 [P] `tests/ChangeOrder.Presentation.Tests/RateLimitTests.cs` — exercises SC-005: the 101st request within a minute returns HTTP 429 within 50 ms with a `Retry-After` header
- [ ] T091a [P] `tests/ChangeOrder.Presentation.Tests/PerformanceTests.cs` — exercises SC-002 under nominal load (≤50 concurrent users, ≤10 RPS sustained): 95% of `POST /api/v1/change-orders` complete in under 3 seconds end-to-end. Use `BenchmarkDotNet` or a hand-rolled load harness pointing at the in-process `WebApplicationFactory` host
- [ ] T091b [P] [US3] `tests/ChangeOrder.Business.Tests/Commands/UpdateOrder/ConcurrencyTests.cs` — exercises FR-013: two `UpdateOrderHandler.HandleAsync` calls with the same starting `RowVersion`; the first succeeds, the second receives `DomainErrors.Order.ConcurrencyConflict` and HTTP 409 (verified end-to-end in a separate `Presentation.Tests` test if needed)
- [ ] T092 [P] `tests/ChangeOrder.Presentation.Tests/HealthCheckTests.cs` — exercises SC-007: `/health` returns 200 when SQL is reachable and 503 when not (using a test double for the SQL health check)
- [ ] T093 Update the project `README.md` quick-start section to mirror `quickstart.md`, plus a "Spec-Driven Development" paragraph referencing `specs/001-change-order-management/` and the constitution
- [ ] T094 Run `dotnet format`, `dotnet build` (must report 0 warnings), `dotnet test` (full suite including Testcontainers); fix any drift before opening the PR

**Final Checkpoint**: All success criteria from `spec.md` (SC-001..SC-007) demonstrable; constitution gates 1-6 still passing.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no deps; can start immediately.
- **Foundational (Phase 2)**: depends on Setup; BLOCKS every user story.
- **US1 (Phase 3, P1)**: depends on Foundational; once green, ships as MVP.
- **US2 (Phase 4, P2)**: depends on Foundational; integrates with US1 (workflow advances an order created by US1) but can be developed in parallel after Foundational.
- **US3 (Phase 5, P3)**: depends on Foundational; mostly independent of US1/US2 but shares the same `ChangeOrderRepository` (no file conflict).
- **Polish (Phase 6)**: depends on all user-story phases being complete (or at least US1, for early production cuts).

### Within each user story

- Models / VOs are produced in Foundational; user-story phases assemble handlers and endpoints on top.
- Tests for a user story run alongside (or after, depending on TDD preference) the implementation tasks of that story; the `[Trait("Category","Testcontainers")]` ones are gated separately in CI.

### Parallel opportunities

- All `[P]` tasks in Phase 1 can run in parallel (different files, no inter-dependencies).
- Phase 2 has many `[P]` slots within `Domain/`; the Data and Business plumbing have sequential dependencies (DbContext depends on entities; Repository depends on DbContext).
- The three user-story phases can be staffed in parallel by different developers once Foundational is green.

---

## Implementation Strategy

### MVP first (User Story 1 only)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories).
3. Complete Phase 3: User Story 1.
4. **STOP and VALIDATE**: smoke test from `quickstart.md` §5 + concurrency test SC-001.
5. Deploy / demo if ready.

### Incremental delivery

1. Setup + Foundational → foundation ready.
2. Add US1 → demo (MVP).
3. Add US2 → demo (governance loop closed).
4. Add US3 → demo (full CRUD).
5. Polish → production cut.

### Parallel team strategy (if multiple devs)

After Foundational:

- Developer A: User Story 1.
- Developer B: User Story 2.
- Developer C: User Story 3.

Stories integrate cleanly because all three share the same repository, the same `OrderMapper`, the same `ProblemDetailsFactory`, and the same endpoint group.

---

## Notes

- `[P]` tasks operate on different files. Where two tasks would touch the same file (e.g., T058 and T068 both edit `EndpointRouteBuilderExtensions.cs`), they are NOT both marked `[P]` — the file is the unit of contention.
- Every task lists an explicit file path. If during implementation a path needs to change, update `plan.md` and `data-model.md` first, then this file.
- Re-evaluate the six Constitution Check gates after each phase. If a gate would fail, **stop** and amend the constitution (PR with `docs(constitution):` scope) before continuing.
- Commit after each task or logical group (per the constitution's pre-commit checklist).
