# ADR-0005: Idempotencia en POST vía header `Idempotency-Key`

- **Status**: Accepted
- **Fecha**: 2026-05-12
- **Decisores**: Jose Lara
- **Tags**: api, http, reliability

## Context

`POST /api/v1/change-orders` crea recursos no idempotentes por naturaleza: cada llamada exitosa produce un número de orden nuevo. Bajo condiciones reales (retries del cliente por timeout, reintentos automáticos en proxies, doble-click en UI), una sola intención de creación puede generar **múltiples órdenes duplicadas**, contaminando la secuencia diaria del formato `yyyyMMdd-##`.

El problema requiere garantías de **at-most-once efectivo desde la perspectiva del cliente**, no exactly-once en infraestructura.

## Decision

Adoptamos el header HTTP **`Idempotency-Key`** como mecanismo de idempotencia en POST:

1. El cliente genera un identificador único por intención de creación (típicamente UUID v4) y lo envía en el header `Idempotency-Key`.
2. El servidor persiste la tupla `(Idempotency-Key, resultado)` durante una ventana de retención.
3. Si llega una segunda petición con la **misma key** dentro de la ventana, el servidor devuelve el **mismo resultado** sin re-ejecutar la creación.
4. La key es opcional desde el punto de vista del contrato; cuando está ausente, la petición se procesa como creación normal sin garantías de idempotencia.
5. La convención de header sigue el patrón establecido por Stripe / IETF draft `draft-ietf-httpapi-idempotency-key-header`.

## Alternatives considered

- **Correlation ID generado por el servidor** — descartada porque el cliente no controla la clave, así que retries del cliente no podrían reusar la misma identidad.
- **Sin idempotencia, dejarlo al cliente** — descartada por el costo de negocio de órdenes duplicadas y la fragilidad ante retries automáticos en infraestructura intermedia.
- **Idempotencia derivada del body (hash del payload)** — descartada porque dos intenciones legítimas con el mismo payload son indistinguibles, lo que llevaría a falsos positivos.
- **`PUT` con ID generado por cliente** — descartada porque rompe el modelo REST del recurso (el número de orden lo genera el servidor según el formato `yyyyMMdd-##`).

## Consequences

### Positivas

- **Retries seguros** del cliente sin generar duplicados.
- **Patrón estándar** ampliamente entendido por equipos de integración.
- **Contrato explícito** documentado en el OpenAPI.

### Negativas

- **Almacenamiento adicional** para la tabla de keys consumidas + ventana de retención.
- **Disciplina del cliente** — la garantía solo aplica si el cliente reusa la misma key en sus retries; un cliente que genera una key nueva por retry pierde la protección.

### Neutras

- La ventana de retención de keys es un parámetro de configuración; valor por defecto y política de purga son responsabilidad de la implementación.

## Compliance / Validación

- Schema del header en `specs/001-change-order-management/contracts/openapi.yaml`.
- Tests de integración deben cubrir: petición con key nueva, petición duplicada con misma key, petición con key vencida.

## Referencias

- IETF draft `draft-ietf-httpapi-idempotency-key-header`.
- `specs/001-change-order-management/contracts/openapi.yaml` — definición del header.
- [ADR-0004](0004-order-number-format-yyyymmdd-99-cap.md) — formato que se protege de duplicación.
