# Architecture Decision Records — ChangeOrder.Api

Este directorio contiene los **Architecture Decision Records (ADRs)** del proyecto: registros inmutables de decisiones arquitectónicas transversales, con su contexto, alternativas evaluadas y consecuencias.

## Por qué existen los ADRs

`Docs/ChangeOrder.Api.Rules.md` documenta las convenciones **vigentes** (qué se hace hoy). Los specs en `specs/<change-id>/` documentan el diseño **por feature**. Los ADRs cubren un hueco distinto: el **por qué histórico** de las decisiones transversales, atadas a una fecha y a un contexto que no se reescribe cuando la decisión cambia.

Cuando un ADR queda obsoleto, **no se borra ni se edita el cuerpo** — se crea un ADR nuevo con status `Accepted` que marca al anterior como `Superseded by ADR-XXXX`. Eso preserva la trazabilidad histórica.

## Convenciones

- **Nombre de archivo**: `NNNN-titulo-en-kebab-case.md` (cuatro dígitos, comenzando en `0001`).
- **`0000-template.md`**: plantilla base, no es un ADR real.
- **Formato**: ver plantilla. Secciones obligatorias: Status, Context, Decision, Consequences.
- **Status válidos**: `Proposed`, `Accepted`, `Deprecated`, `Superseded by ADR-XXXX`.
- **Inmutabilidad**: una vez `Accepted`, el cuerpo no se modifica salvo correcciones tipográficas o de formato. Cambios de criterio se modelan con un ADR nuevo.
- **PR obligatorio**: todo ADR nuevo entra por PR a `main`, con revisión.

## Índice

| ADR | Título | Status | Fecha |
|---|---|---|---|
| [0001](0001-onion-architecture-cqrs.md) | Adopción de Onion Architecture + CQRS | Accepted | 2026-05-18 |
| [0002](0002-result-pattern-retryable-deadlock.md) | Result Pattern retryable para deadlock SQL 1205 | Accepted | 2026-05-14 |
| [0003](0003-unit-of-work-transaction-abstraction.md) | Abstracción `IUnitOfWorkTransaction` | Accepted | 2026-05-13 |
| [0004](0004-order-number-format-yyyymmdd-99-cap.md) | Formato `yyyyMMdd-##` con tope diario en 99 | Accepted | 2026-05-12 |
| [0005](0005-idempotency-key-header-for-post.md) | Idempotencia en POST vía `Idempotency-Key` | Accepted | 2026-05-12 |
| [0006](0006-slnx-solution-format.md) | Adopción del formato `.slnx` | Accepted | 2026-05-12 |
| [0007](0007-manual-docker-image-publishing.md) | Publicación manual de imágenes Docker | Accepted | 2026-05-14 |
| [0008](0008-nuget-http2-environment-workaround.md) | Workaround HTTP/2 + IPv6 para `dotnet restore` | Accepted | 2026-05-12 |

## Cómo añadir un ADR nuevo

1. Crear rama `docs/adr-<slug-corto>` desde `main` limpio.
2. Copiar `0000-template.md` a `NNNN-titulo.md` con el siguiente número correlativo.
3. Rellenar todas las secciones. Status inicial `Proposed`.
4. Actualizar la tabla de índice en este `README.md`.
5. Abrir PR. Tras aprobación y merge, el status pasa a `Accepted`.
