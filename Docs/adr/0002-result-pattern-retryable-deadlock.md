# ADR-0002: Result Pattern retryable para deadlock SQL 1205

- **Status**: Accepted
- **Fecha**: 2026-05-14
- **Decisores**: Jose Lara
- **Tags**: data, error-handling, concurrency, business

## Context

`ChangeOrderRepository.GetNextSequenceForDateAsync` ejecuta SQL crudo con `SELECT … WITH (UPDLOCK, HOLDLOCK)` para reservar la próxima secuencia del día dentro del formato `yyyyMMdd-##`. Bajo carga concurrente real (verificado con 16 workers en `OrderNumberConcurrencyTests`), SQL Server elige una víctima de deadlock y emite el error **1205** **antes** de que se llegue a `SaveChangesAsync`.

Esto rompía el flujo CQRS habitual:

- El `catch (DbUpdateException)` del `UnitOfWork` nunca ve el 1205 porque la excepción viene del `ExecuteSqlRawAsync` en el repositorio, no del `SaveChanges`.
- Una `SqlException` propagada hasta el handler obliga al caller (Presentation) a saber sobre detalles de infraestructura, violando Onion.
- El retry de mediana granularidad necesita una señal **tipada**, no una excepción que puede confundirse con cualquier otra falla SQL.

Se evaluaron tres approaches durante el incidente — referidos internamente como C1, C2 y C3.

## Decision

Adoptamos el **approach C1**: los errores SQL transientes con semántica de retry se codifican como **`Result<T, Error>.Failure(...)`** usando el código de dominio `order.deadlock_victim`, en lugar de propagarse como excepción.

Concretamente:

1. `DomainErrors.Order.DeadlockVictim()` devuelve un `Error` con código `order.deadlock_victim`.
2. `ChangeOrderRepository.GetNextSequenceForDateAsync` atrapa `SqlException` con `Number == 1205` y devuelve `Result<int, Error>.Failure(DomainErrors.Order.DeadlockVictim())`.
3. `CreateOrderHandler.CreateFreshAsync` interpreta ese `Error` como señal retryable y ejecuta su política de reintentos dentro de un `IUnitOfWorkTransaction` fresco por intento.
4. Si los reintentos se agotan, el error se propaga al caller como cualquier otro `Result.Failure` — Presentation lo mapea al status HTTP correspondiente.

Implementado en commit `e56d9af` sobre la rama `001-change-order-management`, posteriormente mergeada a `main`.

## Alternatives considered

- **C2: catch genérico en el `UnitOfWork`** — descartada porque la excepción nace del SQL crudo antes de llegar a `SaveChanges`; el catch del UoW jamás se ejecuta. Habría requerido envolver toda invocación de raw-SQL en infraestructura adicional con la misma forma.
- **C3: propagar `SqlException` hasta Presentation y filtrar por `Number == 1205`** — descartada porque acopla la capa HTTP a un detalle del proveedor SQL Server, y mezcla excepciones de infraestructura con flujo de control esperado. Rompe la convención general del proyecto de usar Result Pattern para errores de dominio.

## Consequences

### Positivas

- **Retry tipado y verificable** — el handler decide la política de retry mirando un `Error.Code` estable, no inspeccionando excepciones.
- **Dominio limpio** — Business no depende de `Microsoft.Data.SqlClient`.
- **Test determinístico** — `OrderNumberConcurrencyTests.NinetyNineConcurrentCreates_Produce99DistinctOrderNumbers` verifica el comportamiento con 99 workers concurrentes.

### Negativas

- **Asimetría conceptual** — un error que técnicamente es una excepción SQL viaja como `Result.Failure`. Hay que documentarlo (este ADR) para que el patrón no sorprenda a nuevos colaboradores.
- **Conversión obligatoria en frontera** — toda nueva operación raw-SQL que pueda producir 1205 debe atrapar y convertir explícitamente.

### Neutras

- El catch específico de `SqlException` queda concentrado en `ChangeOrderRepository`; otras operaciones raw-SQL que se añadan deberán seguir el mismo patrón.

## Compliance / Validación

- Test de integración: `OrderNumberConcurrencyTests.NinetyNineConcurrentCreates_Produce99DistinctOrderNumbers`.
- Code review: cualquier `throw new SqlException` o propagación de `SqlException` desde repositorios debe ser rechazada en PR.
- El código de error `order.deadlock_victim` está documentado en `DomainErrors.Order`.

## Referencias

- Commit `e56d9af` — implementación inicial del approach C1.
- [ADR-0003](0003-unit-of-work-transaction-abstraction.md) — `IUnitOfWorkTransaction` que envuelve cada intento de retry.
- `OrderNumberConcurrencyTests.NinetyNineConcurrentCreates_Produce99DistinctOrderNumbers`.
