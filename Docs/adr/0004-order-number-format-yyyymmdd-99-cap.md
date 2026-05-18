# ADR-0004: Formato de número de orden `yyyyMMdd-##` con tope diario en 99

- **Status**: Accepted
- **Fecha**: 2026-05-12
- **Decisores**: Jose Lara
- **Tags**: domain, business-rule

## Context

El dominio de Change Orders requiere un identificador legible para humanos, secuencial por día, único y compatible con la nomenclatura usada en sistemas previos del cliente. El formato acordado es:

```
yyyyMMdd-##
```

Ejemplo: `20260512-01`, `20260512-02`, … hasta `20260512-99`.

Esto plantea dos preguntas:

1. ¿Qué pasa cuando el contador del día alcanza 99?
2. ¿Se permite ampliar el sufijo a 3+ dígitos si se supera el límite?

El formato es **una restricción de negocio explícita**, no un detalle de implementación. Cambiarlo afectaría integraciones externas, reportes históricos y mecanismos de validación que ya esperan el formato `\d{8}-\d{2}`.

## Decision

Adoptamos el formato `yyyyMMdd-##` con **tope estricto de 99 órdenes por día**:

1. La secuencia diaria opera en el rango `01..99`.
2. Al alcanzar 99, la creación de la orden 100 del día **debe fallar** con un error de dominio explícito (no extender automáticamente a 3 dígitos).
3. El formato es invariante — ningún caso permite emitir `yyyyMMdd-100`, `yyyyMMdd-001` ni variantes.

El límite es **intencional** y es parte del contrato del dominio, no una limitación técnica.

## Alternatives considered

- **Sufijo de 3 dígitos (`yyyyMMdd-###`, 01..999)** — descartada por compatibilidad con sistemas externos que ya consumen el formato `\d{8}-\d{2}` y reportes que asumen ese ancho.
- **Sufijo variable sin padding (`yyyyMMdd-1`, `yyyyMMdd-100`)** — descartada porque rompe ordenamiento lexicográfico y dificulta el parsing.
- **Contador global sin reseteo diario** — descartada porque pierde la legibilidad por fecha que el negocio exige.
- **Permitir extensión automática al superar 99** — descartada porque enmascara un caso de negocio que merece visibilidad explícita (volumen diario anormalmente alto).

## Consequences

### Positivas

- **Formato estable y legible** — humanos pueden leer e identificar la fecha y orden del día sin parsing.
- **Compatible con sistemas externos** que esperan `\d{8}-\d{2}`.
- **El test de concurrencia confirma el contrato** — 99 workers concurrentes producen exactamente 99 números distintos.

### Negativas

- **Techo duro de 99 órdenes por día** — si el negocio crece a ese volumen, requiere un ADR nuevo que supersede a éste para revisar el formato.
- **Error explícito en el cliente** cuando se alcanza el tope — la API debe devolver un código mapeado a HTTP coherente (no `500`).

### Neutras

- El test `NinetyNineConcurrentCreates_Produce99DistinctOrderNumbers` está intencionalmente en 99 workers (no 100) y **no debe extenderse**.

## Compliance / Validación

- Test de concurrencia: `OrderNumberConcurrencyTests.NinetyNineConcurrentCreates_Produce99DistinctOrderNumbers`.
- El error de dominio para el caso de tope superado debe estar tipado y mapeado en Presentation.
- Cualquier propuesta de extender el formato requiere un ADR que supersede a éste.

## Referencias

- `specs/001-change-order-management/data-model.md` — definición del formato.
- `specs/001-change-order-management/contracts/openapi.yaml` — schema de respuesta con el formato.
- [ADR-0002](0002-result-pattern-retryable-deadlock.md) — error retryable que protege la generación bajo concurrencia.
