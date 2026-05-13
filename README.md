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
| Documentacion API | OpenAPI 3.1 / Swagger |
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
ChangeOrder.sln
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

### Entidad principal: `ChangeOrderEntity`

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

Base URL: `/api/v1/change-orders`

| Metodo | Ruta | Descripcion | Respuesta |
|---|---|---|---|
| `GET` | `/` | Listar ordenes (paginado) | `200 OK` |
| `GET` | `/{id:guid}` | Obtener orden por ID | `200 OK` / `404 Not Found` |
| `POST` | `/` | Crear nueva orden | `201 Created` |
| `PUT` | `/{id:guid}` | Actualizar orden | `204 No Content` |
| `DELETE` | `/{id:guid}` | Eliminar orden (soft delete) | `204 No Content` |

### Caracteristicas de la API

- **Paginacion** obligatoria en endpoints de listas (`PagedResponse<T>`).
- **Patron Result\<T, E\>** para manejo de errores de negocio (sin excepciones).
- **CQRS** con Commands (escritura) y Queries (lectura) separados.
- **Validacion** built-in de .NET 10 con `AddValidation()`.
- **Rate Limiting** con ventana fija (100 requests/minuto).
- **Idempotencia** en POST via header `Idempotency-Key`.
- **Health Check** en `/health` (SQL Server).
- **CORS** configurado para clientes internos.
- **Versionado** de API con `Asp.Versioning.Http`.
- **Compresion** de respuestas habilitada.
- **Global Exception Handling** con `ProblemDetails`.

## Requisitos Previos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [SQL Server](https://www.microsoft.com/sql-server) (local o remoto)
- [Docker](https://www.docker.com/) (opcional, para contenedores)

## Inicio Rapido

### 1. Clonar el repositorio

```bash
git clone https://github.com/applicapr/ChangeOrder.git
cd ChangeOrder
```

### 2. Variables de entorno (solo este host)

Si se trabaja en un host afectado por el bug de HTTP/2 ALPN contra `api.nuget.org`
(documentado en `specs/001-change-order-management/research.md` R-10), todo
comando `dotnet` debe ejecutarse con estas variables:

```bash
export DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP2SUPPORT=false
export DOTNET_SYSTEM_NET_DISABLEIPV6=1
```

Los runners de GitHub Actions no requieren estas variables.

### 3. Configurar la base de datos

Editar `src/ChangeOrder.Host/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=ChangeOrderDb;Trusted_Connection=true;TrustServerCertificate=true;"
  }
}
```

### 4. Aplicar migraciones

```bash
dotnet ef database update \
  --project src/ChangeOrder.Data \
  --startup-project src/ChangeOrder.Host
```

### 5. Ejecutar la aplicacion

```bash
dotnet run --project src/ChangeOrder.Host --launch-profile http
```

La API queda disponible en `http://localhost:5151`. En entorno Development se
publican Scalar (`/scalar`) y el documento OpenAPI (`/openapi/v1.json`).

### 6. Probar el endpoint principal

```bash
curl -sS -X POST http://localhost:5151/api/v1/change-orders \
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
          "fullName": "Jane Doe",
          "position": "QA Lead",
          "department": "Quality",
          "email": "jane.doe@example.com"
        }
      }'
```

La respuesta `201 Created` incluye el `orderNumber` con formato `yyyyMMdd-##`.
Reenviar el mismo `Idempotency-Key` con el mismo payload devuelve `200 OK` con
el mismo recurso; con un payload distinto devuelve `422` (`idempotency.payload_divergence`).

### 7. Health check y version

```bash
curl -sS http://localhost:5151/health           # 200 si SQL Server responde
curl -sS http://localhost:5151/version          # { name, version, environment }
```

### 8. Ejecutar tests

```bash
dotnet test                                                   # suite completa
dotnet test --filter "Category!=Testcontainers&Category!=RateLimit"   # CI fast lane
```

Los tests marcados con `[Trait("Category","Testcontainers")]` requieren Docker;
`RateLimit` puede dejarse fuera del lane interactivo porque consume 100+
requests por ventana.

## Docker

### Build y ejecucion

```bash
docker build -t changeorder-api .
docker run -p 8080:8080 changeorder-api
```

### Dockerfile (multi-stage)

El proyecto incluye un Dockerfile optimizado con multi-stage build basado en las imagenes oficiales de .NET 10.

## CI/CD

El pipeline de GitHub Actions (`.github/workflows/ci.yml`) se ejecuta en cada push y PR a `main`:

1. Restore de dependencias
2. Build del proyecto
3. Ejecucion de tests
4. Validacion de reglas de codigo (max 500 lineas por archivo `.cs`)

## Convenciones del Proyecto

- **Conventional Commits**: `feat(orders):`, `fix(data):`, `chore(host):`, `docs(readme):`, `refactor(business):`, `test(business):`
- **Ramas**: `main`, `feature/descripcion`, `fix/descripcion`, `release/vX.Y.Z`
- **PRs obligatorios** para merge a `main` con merge commit (`--no-ff`).
- **Testing**: xUnit + FluentAssertions + NSubstitute
- **Mappers manuales**: sin AutoMapper, metodos estaticos explicitos.

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
| `tasks.md` | Plan de implementacion (T001..T094, fases setup -> polish). |
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
- CORS configurado para maquinas internas.

## Licencia

Este proyecto esta bajo la licencia [MIT](LICENSE).

Copyright (c) 2026 Rafael Alvarez
