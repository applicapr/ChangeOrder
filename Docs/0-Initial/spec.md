# Spec — ChangeOrder.Api

## 1. Resumen ejecutivo

Sistema interno de gestión de **Órdenes de Cambio** (Change Request Management) para aplicaciones en producción. Cuando un cliente solicita un cambio sobre una aplicación productiva, la organización genera una **orden formal** que es evaluada por una cadena de 4 niveles jerárquicos antes de ejecutarse, y queda registrada con trazabilidad completa hasta su despliegue.

## 2. Stakeholders

| Actor | Rol |
|---|---|
| Solicitante (Requester) | Identifica la necesidad y crea la orden |
| Jefe de Departamento | Aprueba la solicitud desde el lado del negocio |
| Jefe de TI | Evalúa impacto técnico y autoriza desde infraestructura |
| División de Programación | Aprobación ejecutiva final; planifica y ejecuta el cambio |

## 3. Requisitos funcionales

### RF-1 — Creación de orden
- Usuario crea orden con: programa afectado, versión actual en producción, screenshot pre-cambio, descripción del trabajo, detalles, justificación y acción requerida.
- El sistema genera automáticamente `OrderNumber` con formato `yyyyMMdd-##` (ej. `20260512-01`), **thread-safe** bajo concurrencia.
- Estado inicial: `Draft`.

### RF-2 — Cadena de aprobación (4 niveles)
- La orden recorre 4 aprobaciones independientes:
  1. Solicitante (auto-confirmación).
  2. Jefe de Departamento.
  3. Jefe de TI.
  4. División de Programación.
- Cada aprobación tiene estado `Pending | Approved | Rejected`.
- Una orden rechazada en cualquier nivel queda bloqueada hasta corrección.

### RF-3 — Tracking de estado (`OrderStatus`)
Transiciones permitidas:
- `Draft → PendingApproval`
- `PendingApproval → Approved | Cancelled`
- `Approved → InProgress`
- `InProgress → Deployed | Cancelled`

### RF-4 — Operaciones CRUD (REST)
- `GET /api/v1/change-orders` — listado paginado.
- `GET /api/v1/change-orders/{id}` — detalle por GUID.
- `POST /api/v1/change-orders` — crear (header `Idempotency-Key` obligatorio).
- `PUT /api/v1/change-orders/{id}` — actualizar (con restricciones por estado).
- `DELETE /api/v1/change-orders/{id}` — soft delete.

### RF-5 — Documentación post-cambio
- Sistema registra: fecha de entrega, evaluación inicial, despliegue a producción y screenshot post-cambio.

## 4. Requisitos no funcionales

| Categoría | Requisito |
|---|---|
| Concurrencia | Generación de `OrderNumber` segura bajo carga simultánea — UNIQUE constraint en BD como red de seguridad |
| Auditoría | `CreatedAt`, `UpdatedAt`, `DeletedAt` automáticos vía interceptor EF Core |
| Trazabilidad | Soft delete obligatorio; jamás eliminación física |
| Performance | Paginación obligatoria, índices en `OrderNumber`, `RequestDate`, `Status`, `IsDeleted` |
| Idempotencia | POST debe ser idempotente vía header `Idempotency-Key` |
| Rate limiting | 100 req/min por cliente, devuelve `429 Too Many Requests` |
| Health check | `/health` verifica conectividad SQL Server |
| Logging | Serilog estructurado, consola + archivo rolling; sin datos sensibles |

## 5. Criterios de aceptación

- [ ] Generación concurrente de 100 órdenes simultáneas produce 100 `OrderNumber` distintos sin colisiones.
- [ ] Soft delete: una orden marcada como borrada no aparece en `GET` listados ni en `GET /{id}`.
- [ ] Idempotencia: el mismo `Idempotency-Key` enviado dos veces devuelve la misma orden (no se crea duplicado).
- [ ] Rate limit: la 101.ª request en un minuto devuelve `429`.
- [ ] OpenAPI 3.1 expone todos los endpoints con sus DTOs documentados y ejemplos.
- [ ] Health check responde `200 Healthy` con SQL Server up; `503 Unhealthy` con SQL Server down.

## 6. Fuera de alcance — Fase 1

- Cliente WPF (planificado Fase 2).
- Notificaciones por email a aprobadores.
- Generación de documentos PDF por número de orden.
- Autenticación / autorización real (en Fase 1: CORS interno + acceso por red corporativa).

## 7. Riesgos identificados

| Riesgo | Mitigación |
|---|---|
| Race condition al generar `OrderNumber` | UNIQUE constraint en BD + retry con backoff |
| Storage de `Idempotency-Key` no definido en la docs | Decidir entre tabla `IdempotencyKeys` o `IDistributedCache` antes de implementar `POST` |
| Matriz de autorización por estado no especificada | Definirla en fase de diseño antes de implementar `Update`/`Delete` |
| Aprobaciones sin notificación | Fase 1 con polling desde cliente; notificaciones quedan para Fase 2 |
