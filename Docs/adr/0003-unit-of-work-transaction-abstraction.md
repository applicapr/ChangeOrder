# ADR-0003: Abstracción `IUnitOfWorkTransaction` para scopes transaccionales explícitos

- **Status**: Accepted
- **Fecha**: 2026-05-13
- **Decisores**: Jose Lara
- **Tags**: domain, data, transactions

## Context

El flujo de creación de Change Orders requiere transacciones explícitas con scope acotado **por intento de retry**, no por operación completa. El motivo: cuando el repositorio reserva la próxima secuencia con `SELECT … WITH (UPDLOCK, HOLDLOCK)` y sufre un deadlock 1205 (ver [ADR-0002](0002-result-pattern-retryable-deadlock.md)), el siguiente intento necesita iniciar **una transacción nueva** — si reutilizara la anterior, el rollback automático del deadlock dejaría el contexto en estado inconsistente.

El problema concreto:

- Exponer `DbContext.Database.BeginTransactionAsync` directamente desde Business filtra `Microsoft.EntityFrameworkCore` a la capa de negocio, rompiendo Onion.
- `TransactionScope` ambient es difícil de razonar bajo retries y combina mal con `async/await` sin configuración explícita.
- Sin abstracción, cada handler que necesita una transacción terminaría replicando el patrón de begin/commit/rollback, con riesgo de fugas.

## Decision

Adoptamos una **abstracción mínima en Domain** para transacciones explícitas, implementada por la capa de datos:

1. **`IUnitOfWorkTransaction : IAsyncDisposable`** vive en `ChangeOrder.Domain/Abstractions/IUnitOfWorkTransaction.cs`. Solo declara `CommitAsync(CancellationToken)` y `RollbackAsync(CancellationToken)`. **Sin dependencias externas** — la interfaz es pura BCL.
2. **`IUnitOfWork.BeginTransactionAsync(CancellationToken)`** devuelve un `IUnitOfWorkTransaction`. El caller usa `await using` para garantizar dispose determinístico (rollback si no se commiteó).
3. **`EfUnitOfWorkTransaction`** en `ChangeOrder.Data` (internal sealed partial) implementa la interfaz envolviendo `IDbContextTransaction` de EF Core.

Patrón de uso en handlers (Business):

```csharp
await using IUnitOfWorkTransaction tx = await _unitOfWork.BeginTransactionAsync(ct);
// operaciones …
if (resultadoOk)
{
    await tx.CommitAsync(ct);
}
// si no se commitea, dispose hace rollback automático
```

## Alternatives considered

- **Exponer `IDbContextTransaction` directamente** — descartada porque obliga a Business a referenciar `Microsoft.EntityFrameworkCore.Storage`, rompiendo Onion.
- **`TransactionScope` ambient** — descartada por la complejidad de configurar `TransactionScopeAsyncFlowOption.Enabled` correctamente en todos los puntos y la dificultad de auditar qué scope está activo bajo retries.
- **Sin abstracción, transacciones internas a cada repositorio** — descartada porque la unidad transaccional natural cruza varios repositorios (reservar secuencia + insertar orden + actualizar contadores), y debe controlarse desde el handler, no desde la capa de datos.

## Consequences

### Positivas

- **Domain neutro a EF Core** — la abstracción no expone tipos de infraestructura.
- **Composición clara con retries** — un `await using` por intento, con commit explícito en la rama feliz.
- **Tests unitarios de handlers** pueden mockear `IUnitOfWorkTransaction` sin spinning up de EF Core.

### Negativas

- **Una indirección extra** sobre lo que EF Core ya ofrece — para handlers triviales sin retry, el patrón puede parecer ceremonioso.
- **Disciplina necesaria** — olvidar `await using` o no llamar a `CommitAsync` deja la transacción huérfana hasta el GC del scope (mitigado por el `using` async).

### Neutras

- `EfUnitOfWorkTransaction` queda como `internal sealed partial` — no es parte de la API pública de `Data`.

## Compliance / Validación

- Verificar en code review que ningún `using Microsoft.EntityFrameworkCore.Storage` aparezca en Business.
- Cualquier nuevo handler que necesite transacción explícita usa `IUnitOfWork.BeginTransactionAsync`.
- Tests de concurrencia confirman que el rollback ocurre correctamente cuando no se commitea.

## Referencias

- `src/ChangeOrder.Domain/Abstractions/IUnitOfWorkTransaction.cs`.
- [ADR-0002](0002-result-pattern-retryable-deadlock.md) — usa esta abstracción para envolver cada intento de retry.
- [ADR-0001](0001-onion-architecture-cqrs.md) — define la regla de Onion que esta abstracción respeta.
