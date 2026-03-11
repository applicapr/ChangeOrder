# ChangeOrder.Api — Reglas y Guía del Proyecto

## 1. Resumen del Proyecto

Sistema de **Control de Órdenes de Cambio** (Change Request Management).  
Cuando un cliente solicita un cambio a una aplicación en producción, se genera un número de orden con formato:

```
yyyyMMdd-## → Ejemplo: 20260224-01
```

El sistema es un **CRUD completo** expuesto como WebAPI con Minimal APIs.

> **Fase 2** (futura): Cliente que consume esta API, genera documentos por número de orden.

---

## 2. Stack Tecnológico

- **.NET 10** (LTS — soporte hasta noviembre 2028)
- **C# 14** (extension members, `field` keyword, null-conditional assignment, partial constructors)
- **ASP.NET Core 10 — Minimal APIs**
- **Entity Framework Core 10** — Code-First
- **SQL Server** (MSSQL)
- **Serilog** (logging estructurado)
- **Swagger / OpenAPI 3.1**
- **Docker**
- **CI/CD con GitHub Actions**

---

## 3. Arquitectura — Onion Architecture

```
        Domain (Core)          ← Sin dependencias externas
            ↑
    Business    Data           ← Dependen SOLO de Domain
        ↑         ↑
      Presentation             ← Depende de Business
        ↑         ↑
    Host (Composition Root)    ← Conecta todo via DI
```

### Proyectos de la solución

| Proyecto | Responsabilidad | Referencias |
|---|---|---|
| `ChangeOrder.Domain` | Entidades, Value Objects, interfaces, enums | Ninguna |
| `ChangeOrder.Business` | Servicios, Handlers CQRS, validaciones | Domain |
| `ChangeOrder.Data` | DbContext, Repositories, Migrations | Domain |
| `ChangeOrder.Presentation` | Endpoints Minimal API, DTOs, Mappers | Business |
| `ChangeOrder.Host` | Program.cs, DI, configuración | Presentation, Data |

### Reglas de dependencia (CRÍTICO)

- **Domain NO referencia** ningún otro proyecto.
- **Business y Data** solo referencian Domain.
- **Presentation** solo referencia Business.
- **Host** es el Composition Root: registra servicios y conecta todas las capas.
- **NUNCA** referenciar capas superiores desde capas inferiores.

---

## 4. Estructura de Carpetas

```
ChangeOrder.sln
│
├── src/
│   ├── ChangeOrder.Domain/
│   │   ├── Entities/
│   │   │   └── ChangeOrder.cs
│   │   ├── ValueObjects/
│   │   │   ├── OrderNumber.cs
│   │   │   ├── RequesterInfo.cs
│   │   │   └── ApprovalChain.cs
│   │   ├── Enums/
│   │   │   ├── ApprovalStatus.cs
│   │   │   └── OrderStatus.cs
│   │   ├── Abstractions/
│   │   │   ├── IChangeOrderRepository.cs
│   │   │   └── IUnitOfWork.cs
│   │   └── Errors/
│   │       └── DomainErrors.cs
│   │
│   ├── ChangeOrder.Business/
│   │   ├── Commands/
│   │   │   ├── CreateOrder/
│   │   │   │   ├── CreateOrderCommand.cs
│   │   │   │   └── CreateOrderHandler.cs
│   │   │   ├── UpdateOrder/
│   │   │   └── DeleteOrder/
│   │   ├── Queries/
│   │   │   ├── GetOrderById/
│   │   │   ├── GetAllOrders/
│   │   │   └── GetOrdersByDate/
│   │   ├── Abstractions/
│   │   │   ├── ICommandHandler.cs
│   │   │   └── IQueryHandler.cs
│   │   └── Services/
│   │       └── OrderNumberGenerator.cs
│   │
│   ├── ChangeOrder.Data/
│   │   ├── Context/
│   │   │   └── ChangeOrderDbContext.cs
│   │   ├── Configurations/
│   │   │   └── ChangeOrderConfiguration.cs
│   │   ├── Repositories/
│   │   │   └── ChangeOrderRepository.cs
│   │   ├── Migrations/
│   │   └── Extensions/
│   │       └── ServiceCollectionExtensions.cs
│   │
│   ├── ChangeOrder.Presentation/
│   │   ├── Endpoints/
│   │   │   └── ChangeOrderEndpoints.cs
│   │   ├── DTOs/
│   │   │   ├── Requests/
│   │   │   │   ├── CreateOrderRequest.cs
│   │   │   │   └── UpdateOrderRequest.cs
│   │   │   └── Responses/
│   │   │       ├── OrderResponse.cs
│   │   │       └── OrderListResponse.cs
│   │   ├── Mappers/
│   │   │   └── OrderMapper.cs
│   │   └── Extensions/
│   │       └── ServiceCollectionExtensions.cs
│   │
│   └── ChangeOrder.Host/
│       ├── Program.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       ├── Dockerfile
│       └── Extensions/
│           └── ServiceCollectionExtensions.cs
│
├── tests/
│   ├── ChangeOrder.Domain.Tests/
│   ├── ChangeOrder.Business.Tests/
│   ├── ChangeOrder.Data.Tests/
│   └── ChangeOrder.Presentation.Tests/
│
├── .github/
│   └── workflows/
│       └── ci.yml
│
├── .gitignore
├── .editorconfig
├── Directory.Build.props
└── README.md
```

---

## 5. Reglas de Código C#

### 5.1 Generales

- **File-scoped namespaces** obligatorio:
  ```csharp
  namespace ChangeOrder.Domain.Entities;   // ✅
  namespace ChangeOrder.Domain.Entities { } // ❌
  ```
- **Primary constructors** cuando sea posible:
  ```csharp
  public class OrderService(IChangeOrderRepository repository); // ✅
  ```
- **Tipos explícitos** cuando el tipo no es evidente. **No usar `var`** si el tipo no es obvio:
  ```csharp
  // ✅ Correcto — tipo explícito cuando no es obvio
  string orderNumber = generator.Generate();
  ChangeOrderEntity? order = await repository.GetByIdAsync(id);
  Result<OrderResponse, Error> result = await handler.HandleAsync(query);
  IReadOnlyList<OrderResponse> responses = mapper.ToResponseList(orders);

  // ✅ Correcto — var cuando el tipo es evidente por el lado derecho
  var orders = new List<ChangeOrder>();
  var logger = new LoggerConfiguration();
  var builder = WebApplication.CreateBuilder(args);

  // ❌ Incorrecto — var cuando el tipo NO es claro
  var result = handler.HandleAsync(query);    // ¿Qué tipo retorna?
  var data = service.Process(input);          // ¿string? object? Result?
  ```
- **Máximo 500 líneas** por archivo `.cs` (excluye líneas en blanco).
- **Máximo 3 parámetros** en constructores, métodos y funciones.
- Excepciones al límite de líneas: archivos autogenerados (`*.g.cs`, `*Designer.cs`), carpetas `bin/`, `obj/`.
- **CancellationToken obligatorio** — TODOS los métodos `async` deben recibir y propagar `CancellationToken`:
  ```csharp
  // ✅ Correcto
  public async Task<Result<OrderResponse, Error>> HandleAsync(
      GetOrderByIdQuery query,
      CancellationToken cancellationToken = default)
  {
      ChangeOrderEntity? order = await _repository
          .GetByIdAsync(query.Id, cancellationToken);
      // ...
  }

  // ❌ Incorrecto — sin CancellationToken
  public async Task<Result<OrderResponse, Error>> HandleAsync(
      GetOrderByIdQuery query)
  {
      ChangeOrderEntity? order = await _repository.GetByIdAsync(query.Id);
  }
  ```
- **No usar `else`** — aplicar Early Return / Guard Clauses:
  ```csharp
  // ✅ Correcto — Early Return
  if (order is null)
      return Result<OrderResponse, Error>.Failure(DomainErrors.Order.NotFound(id));

  OrderResponse response = OrderMapper.ToResponse(order);
  return Result<OrderResponse, Error>.Success(response);

  // ❌ Incorrecto — usando else
  if (order is null)
  {
      return Result<OrderResponse, Error>.Failure(DomainErrors.Order.NotFound(id));
  }
  else
  {
      OrderResponse response = OrderMapper.ToResponse(order);
      return Result<OrderResponse, Error>.Success(response);
  }
  ```

### 5.2 Nombrado

- **Clases y métodos**: `PascalCase` → `OrderService`, `GetById`
- **Interfaces**: prefijo `I` → `IChangeOrderRepository`
- **Variables y parámetros**: `camelCase` → `orderNumber`, `requestDate`
- **Constantes**: `PascalCase` → `MaxOrdersPerDay`
- **Campos privados**: `_camelCase` → `_repository`
- **Archivos**: mismo nombre que la clase → `ChangeOrder.cs`
- **DTOs**: sufijo `Request` / `Response` → `CreateOrderRequest`, `OrderResponse`

### 5.3 SOLID — Resumen rápido

- **S** — Single Responsibility: cada clase hace UNA cosa.
- **O** — Open/Closed: extiende comportamiento sin modificar código existente.
- **L** — Liskov Substitution: usa interfaces; las implementaciones son intercambiables.
- **I** — Interface Segregation: interfaces pequeñas y específicas.
- **D** — Dependency Inversion: depende de abstracciones, nunca de implementaciones concretas.

### 5.4 C# 14 — Features a utilizar

- **Extension members**: para extender tipos sin herencia.
- **`field` keyword**: acceso directo al backing field en propiedades auto-implementadas.
- **Null-conditional assignment** (`??=`): asignación condicional.
- **Partial constructors**: para clases parciales.

---

## 6. Minimal APIs — Convenciones

### 6.1 Organización de Endpoints

Cada grupo de endpoints va en una clase estática con un método de extensión:

```csharp
namespace ChangeOrder.Presentation.Endpoints;

public static class ChangeOrderEndpoints
{
    public static IEndpointRouteBuilder MapChangeOrderEndpoints(
        this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app
            .MapGroup("/api/v1/change-orders")
            .WithTags("Change Orders")
            .WithOpenApi();

        group.MapGet("/", GetAll);
        group.MapGet("/{id:guid}", GetById);
        group.MapPost("/", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);

        return app;
    }

    private static async Task<IResult> GetAll(
        IQueryHandler<GetAllOrdersQuery, List<OrderResponse>> handler)
    {
        // implementación
    }
}
```

### 6.2 Versionado

- Formato de URL: `/api/v1/...`
- Usar `Asp.Versioning.Http` para versionado:
  ```csharp
  builder.Services.AddApiVersioning(options =>
  {
      options.DefaultApiVersion = new ApiVersion(1, 0);
      options.AssumeDefaultVersionWhenUnspecified = true;
      options.ReportApiVersions = true;
  });
  ```

### 6.3 Validación (nuevo en .NET 10)

- Usar `AddValidation()` built-in de .NET 10 para Minimal APIs.
- Personalizar respuestas de error con `IProblemDetailsService`.
- Soporta `DataAnnotations` y `IValidatableObject`.

```csharp
builder.Services.AddValidation();
builder.Services.AddProblemDetails();
```

### 6.4 Respuestas HTTP

- `200 OK` → GET exitoso
- `201 Created` → POST exitoso (incluir header `Location`)
- `204 No Content` → PUT/DELETE exitoso
- `400 Bad Request` → validación fallida
- `404 Not Found` → recurso no existe
- `500 Internal Server Error` → error inesperado

Usar siempre `TypedResults`:
```csharp
return TypedResults.Created($"/api/v1/change-orders/{order.Id}", response);
return TypedResults.NotFound();
return TypedResults.BadRequest(problemDetails);
```

### 6.5 Global Exception Handling

Middleware obligatorio para capturar excepciones no manejadas y devolver `ProblemDetails`. **NUNCA** exponer stack traces en producción.

```csharp
// En ChangeOrder.Host/Middleware/GlobalExceptionHandler.cs
namespace ChangeOrder.Host.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Excepción no manejada: {Message}", exception.Message);

        ProblemDetails problemDetails = new()
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Error interno del servidor",
            Detail = "Ocurrió un error inesperado. Contacte al administrador."
        };

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
```

Registro en `Program.cs`:
```csharp
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Después de builder.Build()
app.UseExceptionHandler();
```

### 6.6 Paginación

TODOS los endpoints que retornan listas DEBEN soportar paginación. Nunca retornar colecciones completas.

```csharp
// Request de paginación
public sealed record PagedRequest(int Page = 1, int PageSize = 10)
{
    public int Page { get; init; } = Page < 1 ? 1 : Page;
    public int PageSize { get; init; } = PageSize > 50 ? 50 : PageSize < 1 ? 10 : PageSize;
}

// Response paginado
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
```

Uso en endpoint:
```csharp
group.MapGet("/", async (
    [AsParameters] PagedRequest request,
    IQueryHandler<GetAllOrdersQuery, PagedResponse<OrderResponse>> handler,
    CancellationToken cancellationToken) =>
{
    // ...
});
```

### 6.7 CORS

Necesario para que el cliente de Fase 2 pueda consumir la API.

```csharp
// En Program.cs
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClient", policy =>
    {
        policy.WithOrigins("https://localhost:5001") // ajustar según ambiente
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Después de builder.Build()
app.UseCors("AllowClient");
```

**Regla**: Nunca usar `AllowAnyOrigin()` en producción.

### 6.8 Health Checks

Obligatorio para Docker y monitoreo. Verifica que la API y sus dependencias estén funcionando.

```csharp
// En Program.cs
builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "sqlserver");

// Después de builder.Build()
app.MapHealthChecks("/health");
```

Paquete necesario: `AspNetCore.HealthChecks.SqlServer`.

---

## 7. Patrón CQRS

### Regla principal

**Commands** (escritura) y **Queries** (lectura) son clases separadas.

### Estructura por feature

```
Commands/
  CreateOrder/
    CreateOrderCommand.cs    ← record con datos de entrada
    CreateOrderHandler.cs    ← lógica de ejecución
Queries/
  GetOrderById/
    GetOrderByIdQuery.cs     ← record con criterio de búsqueda
    GetOrderByIdHandler.cs   ← lógica de consulta
```

### Interfaces base

```csharp
// Command sin retorno de datos
public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    Task<Result<Unit, Error>> HandleAsync(TCommand command);
}

// Command con retorno
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<Result<TResult, Error>> HandleAsync(TCommand command);
}

// Query
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<Result<TResult, Error>> HandleAsync(TQuery query);
}
```

### Reglas CQRS

- Un Command NUNCA retorna listas de datos.
- Una Query NUNCA modifica estado.
- Cada Handler tiene UNA responsabilidad.
- Los Handlers reciben dependencias por constructor (máx. 3 parámetros).

---

## 8. Patrón Result\<T, E\>

### Propósito

Eliminar excepciones para flujo de negocio. Las excepciones son para errores **inesperados** (I/O, red, etc.).

### Implementación base

```csharp
namespace ChangeOrder.Domain;

public sealed class Result<TValue, TError>
{
    private readonly TValue? _value;
    private readonly TError? _error;

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("No value on failure.");

    public TError Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("No error on success.");

    private Result(TValue value)
    {
        _value = value;
        IsSuccess = true;
    }

    private Result(TError error)
    {
        _error = error;
        IsSuccess = false;
    }

    public static Result<TValue, TError> Success(TValue value) => new(value);
    public static Result<TValue, TError> Failure(TError error) => new(error);
}
```

### Errores de dominio

```csharp
namespace ChangeOrder.Domain.Errors;

public sealed record Error(string Code, string Description)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}

public static class DomainErrors
{
    public static class Order
    {
        public static Error NotFound(Guid id) =>
            new("Order.NotFound", $"Orden con Id '{id}' no fue encontrada.");

        public static readonly Error DuplicateNumber =
            new("Order.DuplicateNumber", "Ya existe una orden con ese número.");

        public static readonly Error InvalidDateRange =
            new("Order.InvalidDateRange", "El rango de fechas es inválido.");
    }
}
```

### Uso en Handlers

```csharp
public async Task<Result<OrderResponse, Error>> HandleAsync(
    GetOrderByIdQuery query)
{
    ChangeOrderEntity? order = await _repository.GetByIdAsync(query.Id);

    if (order is null)
        return Result<OrderResponse, Error>
            .Failure(DomainErrors.Order.NotFound(query.Id));

    OrderResponse response = OrderMapper.ToResponse(order);
    return Result<OrderResponse, Error>.Success(response);
}
```

### Uso en Endpoints

```csharp
private static async Task<IResult> GetById(
    Guid id,
    IQueryHandler<GetOrderByIdQuery, OrderResponse> handler)
{
    Result<OrderResponse, Error> result =
        await handler.HandleAsync(new GetOrderByIdQuery(id));

    return result.IsSuccess
        ? TypedResults.Ok(result.Value)
        : TypedResults.NotFound(result.Error);
}
```

---

## 9. Modelo de Dominio

### Entidad principal: ChangeOrderEntity

```csharp
namespace ChangeOrder.Domain.Entities;

public sealed class ChangeOrderEntity
{
    public Guid Id { get; init; }
    public OrderNumber Number { get; private set; }        // yyyyMMdd-##

    // Información del programa
    public string ProgramName { get; set; }
    public string ProductionVersion { get; set; }
    public string VersionScreenshotPath { get; set; }      // Pre-cambio

    // Solicitud
    public DateTime RequestDate { get; init; }
    public RequesterInfo Requester { get; set; }
    public string WorkDescription { get; set; }
    public string RequestDetails { get; set; }
    public string Justification { get; set; }
    public string RequiredAction { get; set; }

    // Aprobaciones
    public ApprovalChain Approvals { get; set; }

    // Fechas de seguimiento
    public DateTime? DeliveryDate { get; set; }
    public DateTime? InitialEvaluationDate { get; set; }
    public DateTime? ProductionDeployDate { get; set; }
    public string? PostDeployScreenshotPath { get; set; }  // Post-cambio

    // Auditoría
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; set; }
}
```

### Value Objects

```csharp
// Número de orden
public sealed record OrderNumber(string Value)
{
    // Formato: yyyyMMdd-##
    public static OrderNumber Create(DateTime date, int sequence) =>
        new($"{date:yyyyMMdd}-{sequence:D2}");
}
```

### Concurrencia en OrderNumber (IMPORTANTE)

El número secuencial por día (`##`) requiere generación **thread-safe** para evitar duplicados.

**Estrategia recomendada**: usar la base de datos como fuente de verdad.

```csharp
// En OrderNumberGenerator (Business layer)
public async Task<OrderNumber> GenerateNextAsync(
    DateTime date,
    CancellationToken cancellationToken = default)
{
    // Query atómica: obtener el máximo secuencial del día + 1
    int nextSequence = await _repository
        .GetNextSequenceForDateAsync(date, cancellationToken);

    return OrderNumber.Create(date, nextSequence);
}

// En el Repository (Data layer) — usar transacción con lock
public async Task<int> GetNextSequenceForDateAsync(
    DateTime date,
    CancellationToken cancellationToken = default)
{
    string datePrefix = date.ToString("yyyyMMdd");

    int maxSequence = await _context.ChangeOrders
        .Where(o => o.Number.Value.StartsWith(datePrefix))
        .Select(o => o.Number.Value)
        .DefaultIfEmpty()
        .MaxAsync(cancellationToken);

    // Parsear el secuencial actual y sumar 1
    // Proteger con UNIQUE constraint en DB para evitar race conditions
    return maxSequence is null ? 1 : int.Parse(maxSequence[9..]) + 1;
}
```

**CRITICAL**: Agregar `UNIQUE constraint` en la columna `OrderNumber` de la tabla para garantizar que la DB rechace duplicados incluso bajo concurrencia.

```csharp
// En ChangeOrderConfiguration.cs
builder.OwnsOne(x => x.Number, n =>
{
    n.HasIndex(p => p.Value).IsUnique();
});

// Información del solicitante
public sealed record RequesterInfo(
    string Name,
    string Position,       // Cargo
    string Department,
    string Email);

// Cadena de aprobación
public sealed record ApprovalChain(
    ApprovalStatus RequesterApproval,
    ApprovalStatus DepartmentHeadApproval,
    ApprovalStatus ItHeadApproval,
    ApprovalStatus ProgrammingDivisionApproval);
```

### Enums

```csharp
public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected
}

public enum OrderStatus
{
    Draft,
    PendingApproval,
    Approved,
    InProgress,
    Deployed,
    Cancelled
}
```

---

## 10. Base de Datos — EF Core 10

### Reglas

- **Code-First** siempre.
- Configuración de entidades en clases separadas (`IEntityTypeConfiguration<T>`), nunca en el DbContext.
- Connection string en `appsettings.Development.json`, **NUNCA** en código.
- Migrations con nombres descriptivos: `dotnet ef migrations add AddChangeOrderTable`.

### Ejemplo de configuración

```csharp
namespace ChangeOrder.Data.Configurations;

public class ChangeOrderConfiguration
    : IEntityTypeConfiguration<ChangeOrderEntity>
{
    public void Configure(EntityTypeBuilder<ChangeOrderEntity> builder)
    {
        builder.ToTable("ChangeOrders");
        builder.HasKey(x => x.Id);

        builder.OwnsOne(x => x.Number, n =>
        {
            n.Property(p => p.Value)
                .HasColumnName("OrderNumber")
                .HasMaxLength(13)
                .IsRequired();
        });

        builder.OwnsOne(x => x.Requester, r =>
        {
            r.Property(p => p.Name).HasMaxLength(150).IsRequired();
            r.Property(p => p.Position).HasMaxLength(100).IsRequired();
            r.Property(p => p.Department).HasMaxLength(100).IsRequired();
            r.Property(p => p.Email).HasMaxLength(200).IsRequired();
        });

        builder.OwnsOne(x => x.Approvals);
    }
}
```

### Soft Delete y Auditoría

Implementar auditoría automática con `SaveChangesInterceptor` de EF Core. NUNCA borrar registros físicamente.

```csharp
// Interfaz en Domain
public interface IAuditable
{
    DateTime CreatedAt { get; }
    DateTime? UpdatedAt { get; set; }
}

public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}
```

```csharp
// Interceptor en Data layer
namespace ChangeOrder.Data.Interceptors;

public sealed class AuditInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        DbContext? context = eventData.Context;
        if (context is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        foreach (EntityEntry<IAuditable> entry in context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }

        foreach (EntityEntry<ISoftDeletable> entry in context.ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State != EntityState.Deleted)
                continue;

            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = DateTime.UtcNow;
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
```

Registro:
```csharp
builder.Services.AddDbContext<ChangeOrderDbContext>((sp, options) =>
    options.UseSqlServer(connectionString)
           .AddInterceptors(new AuditInterceptor()));
```

**Regla**: Agregar `HasQueryFilter(x => !x.IsDeleted)` en las configuraciones de EF para excluir soft-deleted automáticamente.

### Connection String

En `appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=ChangeOrderDb;Trusted_Connection=true;TrustServerCertificate=true;"
  }
}
```

---

## 11. Logging — Serilog

### Configuración

```csharp
// En Program.cs (Host)
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();
```

### Reglas de logging

- **Information**: operaciones exitosas (orden creada, actualizada).
- **Warning**: situaciones inesperadas pero manejables (orden no encontrada).
- **Error**: excepciones capturadas.
- **NUNCA** loggear datos sensibles (emails completos, datos personales sin enmascarar).
- Usar logging estructurado:
  ```csharp
  _logger.LogInformation("Orden {OrderNumber} creada para {Department}",
      order.Number.Value, order.Requester.Department);
  ```

### En `appsettings.json`

```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "logs/log-.txt",
          "rollingInterval": "Day"
        }
      }
    ]
  }
}
```

---

## 12. OpenAPI / Swagger

- OpenAPI **3.1** (default en .NET 10).
- Habilitar XML doc comments en `.csproj`:
  ```xml
  <PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
  ```
- Usar `.WithOpenApi()` y `.WithTags()` en cada grupo de endpoints.
- Documentar cada endpoint con `/// <summary>` XML comments.

---

## 13. Docker

### Dockerfile (multi-stage)

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish src/ChangeOrder.Host/ChangeOrder.Host.csproj \
    -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ChangeOrder.Host.dll"]
```

### .dockerignore

```
**/bin/
**/obj/
**/publish/
**/.git
**/.vs
```

---

## 14. CI/CD — GitHub Actions

### Workflow básico (`.github/workflows/ci.yml`)

- **Trigger**: push a `main`, PRs a `main`.
- **Pasos**: restore → build → test → (opcional) publish.
- Validar que ningún archivo `.cs` exceda 500 líneas.

---

## 15. Git — Convenciones

### Ramas

- `main` — producción estable.
- `feature/descripcion` — nueva funcionalidad.
- `fix/descripcion` — corrección de bug.
- `release/vX.Y.Z` — preparación de release.

### Commits — Conventional Commits

```
feat(orders): agrega endpoint de creación de orden
fix(data): corrige mapping de ApprovalChain
chore(host): actualiza configuración de Serilog
docs(readme): agrega instrucciones de Docker
refactor(business): extrae OrderNumberGenerator a servicio
test(business): agrega tests para CreateOrderHandler
```

### Reglas

- **Siempre merge commit** (`--no-ff`) para preservar historial.
- **NO** hacer `git add` o `git commit` sin revisión previa.
- PRs obligatorios para merge a `main`.

---

## 16. Testing

### Frameworks

- **xUnit** para tests unitarios.
- **FluentAssertions** para assertions legibles.
- **NSubstitute** o **Moq** para mocking.

### Convenciones

- Nombre del método: `MetodoBajoPrueba_Escenario_ResultadoEsperado`
  ```csharp
  public async Task HandleAsync_OrderExists_ReturnsSuccess()
  public async Task HandleAsync_OrderNotFound_ReturnsFailure()
  ```
- Un proyecto de test por capa.
- Mínimo: tests para todos los Handlers (Commands y Queries).

---

## 17. Directory.Build.props

Archivo en la raíz de la solución para configuración compartida:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>14</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
</Project>
```

---

## 18. Paquetes NuGet Principales

### ChangeOrder.Host

- `Serilog.AspNetCore`
- `Serilog.Sinks.Console`
- `Serilog.Sinks.File`
- `Asp.Versioning.Http`
- `AspNetCore.HealthChecks.SqlServer`
- `Microsoft.EntityFrameworkCore.Design` (herramientas)

### ChangeOrder.Data

- `Microsoft.EntityFrameworkCore.SqlServer`
- `Microsoft.EntityFrameworkCore.Tools`

### ChangeOrder.Presentation

- `Microsoft.AspNetCore.OpenApi`

### Mappers — Regla

Usar **mapeo manual** con métodos estáticos de extensión. NO usar AutoMapper ni Mapster para mantener control explícito y evitar "magia" que confunda al junior.

```csharp
namespace ChangeOrder.Presentation.Mappers;

public static class OrderMapper
{
    public static OrderResponse ToResponse(ChangeOrderEntity entity) => new(
        Id: entity.Id,
        OrderNumber: entity.Number.Value,
        ProgramName: entity.ProgramName,
        RequestDate: entity.RequestDate,
        RequesterName: entity.Requester.Name,
        Status: entity.Status.ToString());

    public static IReadOnlyList<OrderResponse> ToResponseList(
        IEnumerable<ChangeOrderEntity> entities) =>
        entities.Select(ToResponse).ToList().AsReadOnly();
}
```

**Regla**: Un mapper por entidad. Nunca poner lógica de negocio en el mapper.

### Tests

- `xUnit`
- `FluentAssertions`
- `NSubstitute`
- `Microsoft.AspNetCore.Mvc.Testing`

---

## 19. .editorconfig

Archivo en la raíz de la solución para forzar estilo de código automáticamente:

```ini
root = true

[*]
indent_style = space
indent_size = 4
end_of_line = crlf
charset = utf-8
trim_trailing_whitespace = true
insert_final_newline = true

[*.cs]
# Namespaces
csharp_style_namespace_declarations = file_scoped:error

# var preferences — NO usar var cuando el tipo no es evidente
csharp_style_var_for_built_in_types = false:warning
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = false:warning

# Expression preferences
csharp_prefer_simple_using_statement = true:suggestion
csharp_style_expression_bodied_methods = when_on_single_line:suggestion
csharp_style_expression_bodied_properties = true:suggestion

# Pattern matching
csharp_style_pattern_matching_over_is_with_cast_check = true:suggestion
csharp_style_pattern_matching_over_as_with_null_check = true:suggestion

# Null checking
csharp_style_throw_expression = true:suggestion
csharp_style_conditional_delegate_call = true:suggestion

# Modifier preferences
dotnet_style_require_accessibility_modifiers = always:warning

# Naming conventions
dotnet_naming_rule.private_fields_should_be_camel_case.severity = warning
dotnet_naming_rule.private_fields_should_be_camel_case.symbols = private_fields
dotnet_naming_rule.private_fields_should_be_camel_case.style = camel_case_underscore

dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private

dotnet_naming_style.camel_case_underscore.required_prefix = _
dotnet_naming_style.camel_case_underscore.capitalization = camel_case

# Organize usings
dotnet_sort_system_directives_first = true
dotnet_separate_import_directive_groups = false

[*.{json,yml,yaml}]
indent_size = 2

[Dockerfile]
indent_size = 2
```

---

## 20. Rate Limiting, Compresión e Idempotencia

### Rate Limiting (built-in .NET)

Proteger la API contra abuso. Configurar límites por endpoint o globalmente.

```csharp
// En Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed", limiter =>
    {
        limiter.PermitLimit = 100;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// Después de builder.Build()
app.UseRateLimiter();
```

### Response Compression

Reducir tamaño de respuestas para mejorar rendimiento.

```csharp
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

app.UseResponseCompression();
```

### Idempotencia en POST

Para prevenir creación de órdenes duplicadas por reintentos del cliente:

- El cliente envía un header `Idempotency-Key: <GUID>` en cada POST.
- El servidor verifica si ya existe una orden con esa key antes de crear.
- Implementar como middleware o filtro en el endpoint de creación.

```csharp
// En el request
public sealed record CreateOrderRequest(
    Guid IdempotencyKey,  // enviado por el cliente
    string ProgramName,
    string ProductionVersion
    // ... demás campos
);
```

---

## 21. Checklist antes de PR

1. [ ] El código compila sin errores ni warnings.
2. [ ] Ningún archivo `.cs` excede 500 líneas.
3. [ ] Constructores y métodos tienen máximo 3 parámetros.
4. [ ] Se usan tipos explícitos donde el tipo no es evidente (`var` solo cuando es obvio).
5. [ ] No se usa `else` — se aplica Early Return.
6. [ ] Todos los métodos `async` reciben `CancellationToken`.
7. [ ] Los tests pasan (`dotnet test`).
8. [ ] Se usaron `TypedResults` en los endpoints.
9. [ ] Los errores de negocio usan `Result<T, E>`, no excepciones.
10. [ ] Logging apropiado (sin datos sensibles).
11. [ ] Commit message sigue Conventional Commits.
12. [ ] Las dependencias entre capas respetan la arquitectura Onion.
13. [ ] Endpoints de listas usan paginación (`PagedResponse<T>`).
14. [ ] Mappers son manuales, sin lógica de negocio.

---

## 22. Repositorio GitHub

El proyecto se gestiona desde un **repositorio en GitHub**. Todo el ciclo de vida (código, PRs, issues, CI/CD, releases) se administra desde ahí.

### Reglas del repo

- Rama `main` protegida: requiere PR aprobado para merge.
- Merge siempre con `--no-ff` (merge commit) para preservar historial.
- CI/CD (GitHub Actions) corre en cada push y PR a `main`.
- Releases se crean con tags versionados (`vX.Y.Z`) y `gh release create`.
- Cada capa del proyecto debe tener su propio `README.md` describiendo su propósito.

### CHANGELOG.md

Obligatorio mantener un `CHANGELOG.md` en la raíz del proyecto siguiendo el estándar [Keep a Changelog](https://keepachangelog.com/).

Formato:

```markdown
# Changelog

Todos los cambios notables de este proyecto se documentan en este archivo.

El formato está basado en [Keep a Changelog](https://keepachangelog.com/es/1.1.0/),
y este proyecto adhiere a [Semantic Versioning](https://semver.org/lang/es/).

## [Unreleased]

### Added
- Endpoint de creación de órdenes de cambio.
- Validación built-in con `AddValidation()`.

### Changed
- Migración de paginación a `PagedResponse<T>`.

### Fixed
- Corrección de concurrencia en generación de OrderNumber.

## [1.0.0] - 2026-XX-XX

### Added
- CRUD completo de órdenes de cambio.
- Generación automática de número de orden (yyyyMMdd-##).
- Flujo de aprobación en cadena (4 niveles).
- Health checks para SQL Server.
- Global exception handling con ProblemDetails.
- Serilog con sinks a consola y archivo.
- Docker support (multi-stage build).
- CI/CD con GitHub Actions.
```

### Categorías permitidas en CHANGELOG

- **Added** — funcionalidad nueva.
- **Changed** — cambios en funcionalidad existente.
- **Deprecated** — funcionalidad que será removida en el futuro.
- **Removed** — funcionalidad eliminada.
- **Fixed** — corrección de bugs.
- **Security** — correcciones de vulnerabilidades.

### Reglas

- Actualizar el `CHANGELOG.md` en **cada PR** antes de merge.
- La sección `[Unreleased]` acumula cambios hasta el próximo release.
- Al crear un release, mover los cambios de `[Unreleased]` a una nueva sección con versión y fecha.
- Usar Semantic Versioning: `MAJOR.MINOR.PATCH`.

---

## 23. Despliegue

La API se desplegará en un **servidor Docker interno de la compañía** (on-premises, no cloud).

- El contenedor corre en la red interna corporativa.
- No está expuesto a internet.
- Configurar `appsettings.Production.json` con connection strings y URLs del ambiente interno.
- Health check (`/health`) debe ser accesible para monitoreo interno.
- CORS debe permitir orígenes de las máquinas internas donde correrá el cliente WPF.

---

## 24. Notas sobre Fase 2

La segunda parte del proyecto será un **cliente WPF con MVVM** (CommunityToolkit.Mvvm, MaterialDesignInXaml) que:

- Consume esta API via `HttpClient` para CRUD completo.
- Genera documentos/reportes por número de orden según solicitud del usuario.
- Se conectará a la API en el servidor Docker interno de la compañía.
- Seguirá la misma arquitectura por capas (Presentation, Business, Data/API Client).
- Las reglas del cliente WPF se definirán en un documento separado.

---

> **Última actualización**: 2026-03-10  
> **Target**: .NET 10 LTS | C# 14 | Minimal APIs
