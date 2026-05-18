# ADR-0001: Adopción de Onion Architecture + CQRS

- **Status**: Accepted
- **Fecha**: 2026-05-18
- **Decisores**: Jose Lara
- **Tags**: architecture, layering, cqrs

## Context

ChangeOrder.Api es un WebAPI sobre .NET 10 / C# 14 que expone CRUD de Change Requests con generación de número de orden `yyyyMMdd-##`, idempotencia en POST y persistencia en SQL Server vía EF Core 10. El sistema necesita:

- **Aislar la lógica de dominio** de detalles de infraestructura (EF Core, ASP.NET, MediatR) para permitir tests rápidos y refactor sin acoplamiento.
- **Separar el modelo de escritura del de lectura** — los comandos (`CreateOrder`, `UpdateOrder`) tienen reglas de negocio, transacciones y validaciones; las queries son lecturas optimizadas con paginación y proyección directa a DTOs.
- **Mantener un único Composition Root** que cablee dependencias, evitando que cualquier proyecto resuelva su grafo por su cuenta.
- **Evitar referencias transitivas** que permitan a la capa de presentación acceder a tipos de datos directamente y romper el flujo CQRS.

Sin una disciplina de capas explícita, históricamente el proyecto habría terminado con `DbContext` filtrándose a controllers y lógica de dominio dispersa entre handlers e infraestructura.

## Decision

Adoptamos **Onion Architecture con CQRS** como estructura base de la solución. La solución se compone de cinco proyectos en `src/` con dependencias estrictamente dirigidas hacia el dominio:

```
Domain  <--  Business  <--  Presentation  <--  Host (Composition Root)
   ^                                            |
   +----------  Data  --------------------------+
```

| Proyecto | Referencias permitidas |
|---|---|
| `ChangeOrder.Domain` | (ninguna) |
| `ChangeOrder.Business` | `Domain` |
| `ChangeOrder.Data` | `Domain` |
| `ChangeOrder.Presentation` | `Business` |
| `ChangeOrder.Host` | `Presentation`, `Data` |

Reglas duras:

- **Domain no referencia nada externo** — ni EF Core, ni MediatR, ni `Microsoft.AspNetCore.*`. Solo BCL.
- **Presentation no referencia Data** — accede a datos solo a través de comandos/queries en Business.
- **Host es el único Composition Root** — toda la DI vive en `Host/Extensions/ServiceCollectionExtensions.cs` y delega a `AddXxx()` por capa.
- **Cada capa expone su propio `Extensions/ServiceCollectionExtensions.cs`** para registrar sus servicios.
- **Sin `ProjectReference` transitivos redundantes** — cada proyecto referencia solo a su vecino inmediato hacia adentro.

CQRS se aplica dentro de Business con carpetas por feature, separando `Commands/` y `Queries/`, con handlers, validators y mapeos colocados juntos por caso de uso.

## Alternatives considered

- **Arquitectura en capas clásica (N-Tier)** — descartada porque permite que Presentation referencie Data directamente, lo cual acopla la API a la persistencia y rompe el aislamiento del dominio. Onion invierte la dirección de las dependencias para eliminar ese acoplamiento.
- **Clean Architecture con Use Cases explícitos** — equivalente conceptual a Onion + CQRS pero con más ceremonia (interactors, request models, presenters). Para el tamaño del proyecto, CQRS sobre MediatR (o equivalente) cubre el mismo objetivo con menos boilerplate.
- **Vertical Slice Architecture pura sin capas** — descartada por el tamaño esperado del dominio y la necesidad de compartir abstracciones (`IUnitOfWork`, repositorios, errores de dominio) entre features. La estructura vertical se conserva **dentro** de Business como organización por feature, pero las capas externas mantienen el grafo Onion.

## Consequences

### Positivas

- **Dominio testeable sin infraestructura** — `ChangeOrder.Domain.Tests` no necesita base de datos ni host.
- **Refactor seguro de infraestructura** — cambiar de EF Core a Dapper, o de SQL Server a PostgreSQL, queda contenido en `Data`.
- **Reglas verificables mecánicamente** — un `dotnet build` falla si alguien añade un `ProjectReference` prohibido.
- **CQRS dentro de Business** habilita optimizar queries (proyecciones, paginación) sin afectar el modelo de comandos.

### Negativas

- **Más proyectos = más ceremonia** — cada feature nueva toca al menos Domain, Business y Presentation.
- **Curva de aprendizaje** para nuevos colaboradores que vienen de N-Tier o de proyectos monolíticos.
- **Riesgo de sobre-abstracción** si se crean interfaces "por las dudas" en Domain sin un caso real que las justifique.

### Neutras

- Cada capa necesita su `ServiceCollectionExtensions.cs`; eso es disciplina, no costo recurrente.
- Las migraciones EF Core se ejecutan con `--project src/ChangeOrder.Data --startup-project src/ChangeOrder.Host`, no con defaults.

## Compliance / Validación

- **Build-time**: el grafo de `ProjectReference` se valida en cada `dotnet build`. Cualquier intento de referenciar `Data` desde `Presentation` rompe la compilación.
- **Code review**: PRs que añadan `using Microsoft.EntityFrameworkCore` en Domain o Business deben ser rechazados.
- **Skill global**: `/dotnet-onion-architecture` define las reglas canónicas y se invoca cuando hay duda sobre dónde colocar un tipo.

## Referencias

- `Docs/ChangeOrder.Api.Rules.md` — convenciones vigentes derivadas de esta decisión.
- `specs/001-change-order-management/plan.md` — primera implementación que aplicó esta estructura.
- `CLAUDE.md` — sección "Arquitectura vigente — Onion + CQRS".
