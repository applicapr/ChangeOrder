# ChangeOrder.Api

Sistema de **Control de Ordenes de Cambio** (Change Request Management) para gestionar solicitudes de cambio a aplicaciones en produccion.

Cuando un cliente solicita un cambio, se genera un numero de orden con formato `yyyyMMdd-##` (ejemplo: `20260224-01`). El sistema expone un CRUD completo como WebAPI con Minimal APIs.

> **Fase 2** (futura): Cliente WPF con MVVM que consume esta API y genera documentos por numero de orden.

## Stack Tecnologico

| Tecnologia | Version |
|---|---|
| .NET | 10 (LTS) |
| C# | 14 |
| ASP.NET Core | 10 — Minimal APIs |
| Entity Framework Core | 10 — Code-First |
| Base de Datos | SQL Server (MSSQL) |
| Logging | Serilog |
| Documentacion API | OpenAPI 3.1 / Scalar (`Scalar.AspNetCore`) |
| Contenedores | Docker |
| CI/CD | GitHub Actions |

## Arquitectura — Onion Architecture

```
        Domain (Core)          <- Sin dependencias externas
            |
    Business    Data           <- Dependen SOLO de Domain
        |         |
      Presentation             <- Depende de Business
        |         |
    Host (Composition Root)    <- Conecta todo via DI
```

| Proyecto | Responsabilidad | Referencias |
|---|---|---|
| `ChangeOrder.Domain` | Entidades, Value Objects, interfaces, enums | Ninguna |
| `ChangeOrder.Business` | Servicios, Handlers CQRS, validaciones | Domain |
| `ChangeOrder.Data` | DbContext, Repositories, Migrations | Domain |
| `ChangeOrder.Presentation` | Endpoints Minimal API, DTOs, Mappers | Business |
| `ChangeOrder.Host` | Program.cs, DI, configuracion | Presentation, Data |

## Estructura del Proyecto

```
ChangeOrder.slnx
|
+-- src/
|   +-- ChangeOrder.Domain/         # Entidades, Value Objects, Enums, Abstracciones
|   +-- ChangeOrder.Business/       # Commands, Queries (CQRS), Services
|   +-- ChangeOrder.Data/           # DbContext, Configurations, Repositories, Migrations
|   +-- ChangeOrder.Presentation/   # Endpoints, DTOs, Mappers
|   +-- ChangeOrder.Host/           # Program.cs, DI, Middleware, Dockerfile
|
+-- tests/
|   +-- ChangeOrder.Domain.Tests/
|   +-- ChangeOrder.Business.Tests/
|   +-- ChangeOrder.Data.Tests/
|   +-- ChangeOrder.Presentation.Tests/
|
+-- Docs/                           # Documentacion del proyecto
+-- .github/workflows/ci.yml        # CI/CD pipeline
+-- Directory.Build.props            # Configuracion compartida (.NET 10 / C# 14)
+-- .editorconfig                    # Estilo de codigo
```

## Modelo de Dominio

### Entidad principal: `ChangeOrder`

- **Numero de orden** (`OrderNumber`): formato `yyyyMMdd-##`, generado automaticamente con secuencial thread-safe por dia.
- **Informacion del programa**: nombre, version en produccion, screenshot pre-cambio.
- **Solicitud**: fecha, solicitante (`RequesterInfo`), descripcion del trabajo, detalles, justificacion, accion requerida.
- **Aprobaciones** (`ApprovalChain`): cadena de 4 niveles — Solicitante, Jefe de Departamento, Jefe de TI, Division de Programacion.
- **Seguimiento**: fechas de entrega, evaluacion inicial, despliegue a produccion, screenshot post-cambio.
- **Auditoria**: `CreatedAt`, `UpdatedAt`, soft delete.

### Enums

- `ApprovalStatus`: Pending, Approved, Rejected
- `OrderStatus`: Draft, PendingApproval, Approved, InProgress, Deployed, Cancelled

## API Endpoints

Versionado bajo `/api/v1/change-orders`. Los endpoints `health`, `version`, `openapi` y `scalar` viven fuera del grupo versionado.

| Metodo | Ruta | Descripcion | Respuesta |
|---|---|---|---|
| `GET` | `/api/v1/change-orders` | Listar ordenes paginado; soporta `?page=`, `?pageSize=` y `?orderNumber=` (prefix) | `200 OK` |
| `GET` | `/api/v1/change-orders/{id:guid}` | Obtener orden por id | `200` / `404` |
| `POST` | `/api/v1/change-orders` | Crear orden (requiere header `Idempotency-Key`) | `201` / `200` (replay) / `400` / `422` |
| `PUT` | `/api/v1/change-orders/{id:guid}` | Actualizar orden (solo en `Draft`, optimistic concurrency via `rowVersion`) | `204` / `400` / `404` / `409` |
| `DELETE` | `/api/v1/change-orders/{id:guid}` | Soft-delete | `204` / `404` |
| `PUT` | `/api/v1/change-orders/{id:guid}/approvals/{level}` | Registrar verdict en uno de los 4 niveles (`requester`, `departmentHead`, `itHead`, `programmingDivision`) | `204` / `400` / `404` / `409` |
| `PATCH` | `/api/v1/change-orders/{id:guid}/dates` | Setear fechas (`deliveryDate`, `initialEvaluationDate`, `productionDeployDate`) — dispara transiciones de estado | `204` / `404` / `409` |
| `GET` | `/health` | Healthcheck (SQL Server) | `200` / `503` |
| `GET` | `/version` | Identidad del build (`name`, `version`, `environment`) | `200` |
| `GET` | `/openapi/v1.json` | Documento OpenAPI 3.1 (Development) | `200` |
| `GET` | `/scalar/v1` | UI interactiva Scalar (Development) | `200` |

### Caracteristicas de la API

- **Paginacion** obligatoria en endpoints de listas (`PagedResponse<T>`), con `pageSize` acotado a [1..50].
- **Filtro** opcional por `OrderNumber` con prefix-match (`?orderNumber=20260513-02` para exact lookup o `?orderNumber=20260513` para todas las del dia).
- **Patron Result\<T, E\>** para manejo de errores de negocio — handlers NO lanzan excepciones para flow control.
- **CQRS** con Commands (escritura) y Queries (lectura) separados.
- **Validacion** manual via `static partial class` + `[GeneratedRegex]`. `Microsoft.Extensions.Validation` de .NET 10 fue descartado porque su API requiere referenciar `Microsoft.AspNetCore.Http` desde Business, lo que romperia Onion.
- **Rate Limiting** built-in de .NET 10, ventana fija (100 requests/minuto por IP) con header `Retry-After`.
- **Idempotencia** en POST via header `Idempotency-Key` (SHA-256 del payload canonicalizado, retencion 24h).
- **Optimistic concurrency** en `PUT` via SQL Server `rowversion` (FR-013).
- **Soft delete** con global query filter de EF Core; las filas borradas quedan en la tabla pero invisibles para `GET`/listing.
- **Healthcheck** en `/health` que verifica conectividad a SQL Server.
- **Version endpoint** en `/version` para que monitoring tools probeen la identidad del build sin negotiate API version.
- **Versionado** de API con `Asp.Versioning.Http`.
- **ProblemDetails RFC 7807** mapeado por `ProblemDetailsFactory`. Cada `Error.Code` de Domain se traduce a un payload con `type`, `title`, `status`, `detail`, `instance` y `code`.
- **Background service** `IdempotencyCleanupService` corre cada hora y limpia keys >24h via `ExecuteDeleteAsync`.

> Notas: CORS y response compression todavia NO estan configurados. Si se necesitan, agregarlos en `AddPresentationLayer`/`Program.cs`.

## Requisitos Previos

- [Docker Desktop](https://www.docker.com/) para el flujo recomendado (stack completo).
- [.NET 10 SDK](https://dotnet.microsoft.com/download) si preferis correr la API fuera de docker (modo dev local).

## Inicio Rapido (recomendado): Docker Compose

El stack incluye SQL Server 2022 + sidecar de migraciones + la API. Levanta todo con un solo comando.

### 1. Clonar el repositorio

```bash
git clone https://github.com/applicapr/ChangeOrder.git
cd ChangeOrder
```

### 2. Crear el archivo `.env`

```bash
cp .env.example .env
```

El template trae un `SA_PASSWORD` por defecto compatible con la politica de SQL Server. `.env` esta gitignored — nunca commitearlo.

### 3. Levantar el stack

```bash
docker compose up --build
```

La primera vez tarda ~3-5 min (pull de `mcr.microsoft.com/mssql/server:2022-latest` + `mcr.microsoft.com/dotnet/sdk:10.0` + build de la imagen de la API). El sidecar `migrations` corre `dotnet ef database update` y termina; la API arranca recien cuando termina la migracion.

### 4. URLs disponibles

| URL | Que sirve |
|---|---|
| `http://localhost:18080/health` | Healthcheck (200 si SQL Server responde) |
| `http://localhost:18080/version` | `{ name, version, environment }` |
| `http://localhost:18080/scalar/v1` | UI interactiva Scalar (solo Development) |
| `http://localhost:18080/openapi/v1.json` | Documento OpenAPI 3.1 (solo Development) |
| `localhost:14330` | SQL Server (user `sa`, password del `.env`, `TrustServerCertificate=true`) |

> Los puertos `18080` y `14330` se eligieron para no colisionar con los defaults `8080` y `1433` ya en uso por otros containers locales.

### 5. Crear una orden de prueba

```bash
curl -sS -X POST http://localhost:18080/api/v1/change-orders \
  -H 'Content-Type: application/json' \
  -H 'Idempotency-Key: demo-20260513-001' \
  -d '{
        "programName": "BillingApp",
        "productionVersion": "v1.0.0",
        "workDescription": "Fix rounding bug",
        "requestDetails": "Add half-even rounding to totals.",
        "justification": "Customer complaints about cents off.",
        "requiredAction": "Patch Module B, redeploy.",
        "requester": {
          "name": "Jane Doe",
          "position": "QA Lead",
          "department": "Quality",
          "email": "jane.doe@example.com"
        }
      }'
```

La respuesta `201 Created` incluye el `orderNumber` con formato `yyyyMMdd-##`.
Reenviar el mismo `Idempotency-Key` con el **mismo payload** devuelve `200 OK` con
el mismo recurso; con un payload distinto devuelve `422` (`idempotency.payload_divergence`).

### 6. Operacion del stack

```bash
docker compose logs -f api          # logs de la API en vivo
docker compose ps                   # estado de los servicios
docker compose stop                 # parar (la DB persiste en el volumen)
docker compose start                # arrancar de nuevo
docker compose down                 # destruir containers (DB sigue en el volumen)
docker compose down -v              # destruir todo, incluido el volumen de SQL Server
```

## Modo dev local (sin Docker)

Si preferis correr la API fuera de docker — por ejemplo para attach del debugger desde tu IDE:

### 1. Variables de entorno (solo este host)

En hosts afectados por el bug de HTTP/2 ALPN contra `api.nuget.org` (documentado en `specs/001-change-order-management/research.md` R-10), todo comando `dotnet` debe ejecutarse con estas variables. Los runners de GitHub Actions no las requieren.

```bash
export DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP2SUPPORT=false
export DOTNET_SYSTEM_NET_DISABLEIPV6=1
```

### 2. Configurar la base de datos

Editar `src/ChangeOrder.Host/appsettings.Development.json` con la connection string. Para apuntar al SQL Server del docker-compose:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,14330;Database=ChangeOrder;User Id=sa;Password=<SA_PASSWORD del .env>;TrustServerCertificate=true;Encrypt=false;"
  }
}
```

O contra un SQL Server local nativo:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=ChangeOrderDb;Trusted_Connection=true;TrustServerCertificate=true;"
  }
}
```

### 3. Aplicar migraciones

```bash
dotnet ef database update \
  --project src/ChangeOrder.Data \
  --startup-project src/ChangeOrder.Host
```

### 4. Ejecutar la aplicacion

```bash
dotnet run --project src/ChangeOrder.Host --launch-profile http
```

La API queda en `http://localhost:5151`. Mismas URLs operacionales (`/health`, `/version`, `/scalar/v1`, `/openapi/v1.json`) que en el modo docker.

### 5. Ejecutar tests

```bash
dotnet test                                                                       # suite completa
dotnet test --filter "Category!=Testcontainers&Category!=RateLimit&Category!=Performance"   # CI fast lane
```

Categorias gateadas:
- `Testcontainers` — requieren Docker daemon activo (T061 SC-001 concurrencia real).
- `RateLimit` — consume >100 requests por ventana; mejor fuera del lane interactivo.
- `Performance` — load test (T091a SC-002 p95 < 3s).

## Docker (imagen sola, sin compose)

Si solo querias la imagen de la API:

```bash
docker build -f src/ChangeOrder.Host/Dockerfile -t changeorder-api .
docker run -e ConnectionStrings__DefaultConnection='<connection string>' -p 8080:8080 changeorder-api
```

El Dockerfile es multi-stage basado en las imagenes oficiales `mcr.microsoft.com/dotnet/sdk:10.0` (build) y `mcr.microsoft.com/dotnet/aspnet:10.0` (runtime), con usuario no-root y puerto interno 8080.

## CI/CD

El pipeline de GitHub Actions (`.github/workflows/ci.yml`) se ejecuta en cada push y PR a `main`:

1. Restore de dependencias
2. Build del proyecto
3. Ejecucion de tests
4. Validacion de reglas de codigo (max 500 lineas por archivo `.cs`)

## Convenciones del Proyecto

- **Conventional Commits con scope**. Scopes usados en la rama `001-change-order-management`: `feat(bootstrap):`, `feat(foundational):`, `feat(us1):`, `feat(us2):`, `feat(us3):`, `feat(presentation):`, `feat(infra):`, `feat(host):`, `feat(query):`, `feat(polish):`, `fix(data):`, `fix(host):`, `docs(spec):`, `docs(tasks):`, `chore(repo):`.
- **Ramas**: `main`, `NNN-<feature>` (creada por el hook `before_specify` de Spec Kit), `feature/<descripcion>`, `fix/<descripcion>`, `release/vX.Y.Z`.
- **PRs obligatorios** para merge a `main` con merge commit (`--no-ff`).
- **Testing**: xUnit + FluentAssertions + NSubstitute + Testcontainers (gated).
- **Mappers manuales**: sin AutoMapper / Mapster, solo metodos estaticos `static class .ToCommand(...)`/`.ToResponse(...)`.
- **Result Pattern**: handlers devuelven `Result<T, Error>`; nada de excepciones para flow control. Excepciones reservadas para fallos de infraestructura.

## Spec-Driven Development

Este repositorio sigue el flujo **Spec-Driven Development (Spec Kit)**: cada
feature se planifica como un conjunto inmutable de artefactos en
`specs/<feature-id>/` antes de escribir codigo. La feature actual es
`001-change-order-management` (rama `001-change-order-management`).

Artefactos por feature:

| Archivo | Proposito |
|---|---|
| `spec.md` | Requisitos funcionales (FR) y criterios de exito (SC). |
| `plan.md` | Plan tecnico y gates constitucionales. |
| `research.md` | Decisiones tecnicas (R-1..R-10) con alternativas. |
| `data-model.md` | Entidades, value objects, enums. |
| `contracts/openapi.yaml` | Contrato OpenAPI 3.1 autoritativo. |
| `quickstart.md` | Pasos para levantar la feature en local. |
| `tasks.md` | Plan de implementacion (T001..T094 + T088a `/version` + T088b filtro `?orderNumber=`, en 6 fases setup -> polish). |
| `checklists/` | Listas de calidad por dominio (api, data-model, security, completeness). |

La **constitucion** del proyecto vive en `.specify/memory/constitution.md` y
fija los seis principios no negociables (Domain puro, Onion estricto, Result
Pattern, etc.). Cada `plan.md` declara explicitamente que esos gates se
mantienen verdes.

Comandos `/speckit-*` orquestan el ciclo:

```
/speckit-specify     # crea spec.md
/speckit-clarify     # resuelve preguntas abiertas
/speckit-plan        # genera plan + research + data-model + contracts + quickstart
/speckit-tasks       # produce tasks.md ordenado por dependencias
/speckit-analyze     # auditoria cruzada de los artefactos
/speckit-implement   # ejecuta tasks.md en orden
```

Para revisar el estado actual de la implementacion: `specs/001-change-order-management/tasks.md`.

## Documentacion

La carpeta `Docs/` contiene documentacion detallada:

- `ChangeOrder.Api.Rules.md` — Reglas y guia completa del proyecto.
- `ChangeOrder_DataModel.pdf` — Modelo de datos.
- `ChangeOrder_Programmer_Guide.pdf` — Guia del programador.

## Despliegue

La API se despliega en un **servidor Docker interno** (on-premises):

- No esta expuesta a internet.
- Health check en `/health` para monitoreo interno.
- Logs Serilog en formato JSON compacto (`appsettings.Production.json`) con rolling diario (50 MB cap, 30 dias de retencion).
- CORS NO esta configurado todavia; si los clientes lo necesitan, hay que sumarlo a `AddPresentationLayer` antes del despliegue final.

## Licencia

Este proyecto esta bajo la licencia [MIT](LICENSE).

Copyright (c) 2026 Rafael Alvarez
