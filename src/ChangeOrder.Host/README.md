# ChangeOrder.Host

Composition Root del sistema — punto de arranque de la aplicación. Conecta
todas las capas mediante Inyección de Dependencias, configura Serilog,
OpenAPI/Scalar y expone la Web API.

## Responsabilidad

Es la capa más externa de la arquitectura Onion. Registra los servicios de
`Data`, `Business` y `Presentation` en el contenedor de DI, configura el
pipeline HTTP y arranca el servidor Kestrel.

## Estructura

```
ChangeOrder.Host/

├── Program.cs                          # Punto de entrada — DI, pipeline, arranque

├── appsettings.json                    # Configuración base + Serilog

├── appsettings.Development.json        # Connection string local (gitignored)

├── Properties/

│   └── launchSettings.json             # Puertos y perfiles de ejecución

├── Dockerfile                          # Multi-stage build para despliegue

└── Extensions/

└── ServiceCollectionExtensions.cs      # Extensiones específicas del Host
```

## Componentes

### `Program.cs`

Orquesta el arranque completo de la aplicación en 4 pasos:

1. **Serilog** — configurado vía `UseSerilog` leyendo desde `appsettings.json`.
   Escribe a consola y a `logs/log-.txt` con rolling diario.

2. **Registro de servicios por capa** — cada capa expone su propio método de
   extensión:
```csharp
   builder.Services.AddDataServices(connectionString);
   builder.Services.AddBusinessServices();
   builder.Services.AddPresentationServices();
```

3. **OpenAPI** — generado con `AddOpenApi()`, incluye un
   `AddDocumentTransformer` que define título, versión y descripción del
   documento.

4. **Pipeline HTTP** — en `Development` expone:
   - `/openapi/v1.json` vía `MapOpenApi()`
   - `/scalar/v1` vía `MapScalarApiReference()` (UI interactiva)

   Finalmente `MapPresentationEndpoints()` registra todos los endpoints de
   `ChangeOrder.Presentation`.

### `appsettings.json`

Configuración base compartida por todos los entornos. Incluye:

- `AllowedHosts`
- Configuración de **Serilog**: nivel mínimo `Information`, overrides para
  `Microsoft`/`System` en `Warning`, sinks de `Console` y `File`
  (`logs/log-.txt`, rolling diario).

### `appsettings.Development.json`

Contiene la `ConnectionStrings:DefaultConnection` para SQL Server local.
**Gitignored** — nunca se sube al repositorio. Cada desarrollador configura
su propia connection string localmente.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=ChangeOrderDb;Trusted_Connection=true;TrustServerCertificate=true;"
  }
}
```

### `Properties/launchSettings.json`

Define los perfiles de ejecución `http` y `https` con sus puertos
(`5151`/`7151`) y la variable `ASPNETCORE_ENVIRONMENT=Development`.

### `Dockerfile`

Build multi-stage:

- **build** — `mcr.microsoft.com/dotnet/sdk:10.0`, restaura y publica
  `ChangeOrder.Host.csproj`
- **final** — `mcr.microsoft.com/dotnet/aspnet:10.0`, copia el publish y
  expone el puerto `8080`

### `ServiceCollectionExtensions`

Punto de extensión para registrar servicios específicos del Host que no
pertenecen a ninguna otra capa (actualmente vacío, reservado para uso
futuro).

## Configuración del `.csproj`

Incluye dos ajustes específicos para .NET 10 + OpenAPI:

```xml
<NoWarn>$(NoWarn);CS9137</NoWarn>
<InterceptorsNamespaces>$(InterceptorsNamespaces);Microsoft.AspNetCore.OpenApi.Generated</InterceptorsNamespaces>
```

Necesarios porque el generador de interceptores de OpenAPI requiere
habilitar explícitamente su namespace, y de otro modo el build falla con
`CS9137`.

## Paquetes NuGet

| Paquete | Propósito |
|---|---|
| `Serilog.AspNetCore` | Logging estructurado |
| `Serilog.Sinks.Console` | Sink de consola |
| `Serilog.Sinks.File` | Sink de archivo con rolling diario |
| `Scalar.AspNetCore` | UI interactiva de documentación de API |
| `Asp.Versioning.Http` | Versionado de API (`/api/v1/...`) |
| `AspNetCore.HealthChecks.SqlServer` | Healthcheck de conectividad a SQL Server |
| `Microsoft.EntityFrameworkCore.Design` | Herramientas para `dotnet ef` (migraciones) |

## Base de Datos y Migraciones

La base de datos `ChangeOrderDb` se gestiona con EF Core Migrations desde
`ChangeOrder.Data`, usando `ChangeOrder.Host` como startup project:

```powershell
Add-Migration <NombreMigracion> -Project ChangeOrder.Data -StartupProject ChangeOrder.Host
Update-Database -Project ChangeOrder.Data -StartupProject ChangeOrder.Host
```

## URLs disponibles (Development)

| URL | Descripción |
|---|---|
| `http://localhost:5151/api/v1/change-orders` | Endpoints CRUD de órdenes de cambio |
| `http://localhost:5151/scalar/v1` | UI interactiva Scalar |
| `http://localhost:5151/openapi/v1.json` | Documento OpenAPI 3.1 |

## Reglas

- Esta capa referencia `ChangeOrder.Presentation` y `ChangeOrder.Data`.
- Es el único lugar donde se conoce la connection string real.
- Nunca commitear `appsettings.Development.json` con credenciales reales.
- Cualquier nuevo registro de DI debe agregarse al método de extensión de
  la capa correspondiente, no directamente en `Program.cs`.
