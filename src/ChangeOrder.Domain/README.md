# ChangeOrder.Domain

Núcleo del sistema — contiene toda la lógica de negocio pura sin dependencias externas.
Esta capa no referencia ningún otro proyecto de la solución.

## Responsabilidad

Define el modelo de dominio completo del sistema de Control de Órdenes de Cambio:
entidades, value objects, enums, interfaces e interfaces de auditoría.

## Estructura

ChangeOrder.Domain/
├── Entities/
│   └── ChangeOrderEntity.cs       # Aggregate Root — entidad principal
├── ValueObjects/
│   ├── OrderNumber.cs             # Número de orden formato yyyyMMdd-##
│   ├── RequesterInfo.cs           # Datos del solicitante
│   └── ApprovalChain.cs           # Cadena de 4 aprobaciones
├── Enums/
│   ├── ApprovalStatus.cs          # Pending | Approved | Rejected
│   └── OrderStatus.cs             # Draft | PendingApproval | Approved | InProgress | Deployed | Cancelled
├── Abstractions/
│   ├── IChangeOrderRepository.cs  # Contrato de acceso a datos
│   ├── IUnitOfWork.cs             # Contrato de transacciones atómicas
│   ├── IAuditable.cs              # Contrato de auditoría
│   └── ISoftDeletable.cs          # Contrato de borrado lógico
└── Errors/
├── Error.cs                       # Record base de errores de negocio
└── DomainErrors.cs                # Errores específicos del dominio

## Componentes

### Entidad Principal — `ChangeOrderEntity`

Aggregate Root que representa una solicitud de cambio completa. Implementa
`IAuditable` e `ISoftDeletable` para auditoría y borrado lógico automáticos.

Propiedades principales:
- `Id` — identificador único (Guid generado en la aplicación)
- `Number` — número de orden (Value Object)
- `Requester` — datos del solicitante (Value Object)
- `Approval` — cadena de aprobación (Value Object)
- `Status` — estado del ciclo de vida (Enum)

### Value Objects

| Clase           | Descripción                                                                                                 |
|-----------------|-------------------------------------------------------------------------------------------------------------|
| `OrderNumber`   | Encapsula el número con formato `yyyyMMdd-##`. Inmutable, generado con `OrderNumber.Create(date, sequence)` |
| `RequesterInfo` | Agrupa Name, Position, Department y Email del solicitante                                                   |
| `ApprovalChain` | Agrupa las 4 aprobaciones jerárquicas, todas inician en `Pending`                                           |

### Enums

| Enum             | Valores                                                                            |
|------------------|------------------------------------------------------------------------------------|
| `ApprovalStatus` | `Pending` · `Approved` · `Rejected`                                                |
| `OrderStatus`    | `Draft` · `PendingApproval` · `Approved` · `InProgress` · `Deployed` · `Cancelled` |

### Interfaces

| Interfaz                 | Descripción                                                            |
|--------------------------|------------------------------------------------------------------------|
| `IChangeOrderRepository` | Contrato CRUD para acceso a datos — implementado en la capa Data       |
| `IUnitOfWork`            | Contrato de transacciones atómicas — implementado por el DbContext     |
| `IAuditable`             | Expone `CreatedAt` y `UpdatedAt` — gestionados por el AuditInterceptor |
| `ISoftDeletable`         | Expone `IsDeleted` y `DeletedAt` — gestionados por el AuditInterceptor |

### Errores de Dominio

Los errores de negocio se representan con el record `Error(Code, Message)` y se
centralizan en `DomainErrors`. No se usan excepciones para flujo de negocio.

```csharp
DomainErrors.Order.NotFound      // "Order.NotFound"
DomainErrors.Order.AlreadyExists // "Order.AlreadyExists"
```

## Reglas

- Esta capa **no referencia** ningún otro proyecto de la solución.
- Los Value Objects son `sealed record` — inmutables y con igualdad por valor.
- La entidad nunca se borra físicamente — siempre soft-delete vía `ISoftDeletable`.
- Los errores de negocio usan `Error` — nunca excepciones.
