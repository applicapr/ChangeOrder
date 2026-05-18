# ADR-0006: Adopción del formato `.slnx` para el archivo de solución

- **Status**: Accepted
- **Fecha**: 2026-05-12
- **Decisores**: Jose Lara
- **Tags**: tooling, build

## Context

El proyecto se inicia bajo .NET 10 / Visual Studio 2026, momento en el que el formato XML de solución (`.slnx`) está disponible como reemplazo estable del formato legacy `.sln`. La pregunta es cuál adoptar como **único** archivo de solución del repositorio.

El formato `.sln` clásico tiene problemas conocidos:

- Sintaxis propietaria, difícil de leer y editar manualmente.
- Diffs ruidosos en PRs (GUIDs, líneas de configuración por proyecto).
- Herramientas de edición limitadas fuera de Visual Studio.

## Decision

Adoptamos **`ChangeOrder.slnx`** como único archivo de solución del repositorio. El formato `.sln` no se mantiene en paralelo.

Implicaciones operativas:

1. Todos los comandos `dotnet` (build, test, restore, format) usan el `.slnx` automáticamente al estar en la raíz del repo.
2. Cualquier IDE o herramienta usada por el equipo debe soportar `.slnx`. Versiones de Rider / VS / `dotnet` CLI anteriores a la ventana de soporte no son válidas para este repo.
3. Si una herramienta de terceros exige `.sln`, se genera localmente y **no se commitea**.

## Alternatives considered

- **Mantener `.sln` clásico** — descartada por los problemas de diff y por la dirección oficial del ecosistema .NET hacia `.slnx`.
- **Mantener ambos en paralelo** — descartada por la carga de sincronización y el riesgo de divergencia entre archivos.

## Consequences

### Positivas

- **Diffs limpios en PRs** — XML legible, cambios estructurales evidentes.
- **Edición manual viable** — añadir o quitar proyectos sin necesidad del IDE.
- **Alineado con la dirección oficial** del tooling .NET.

### Negativas

- **Requisito de versión mínima** en el tooling del equipo — IDEs y CLIs anteriores no abren el archivo.
- **Documentación dispersa** del formato en el momento de adopción — algunas funcionalidades exigen consultar release notes.

### Neutras

- El archivo vive en la raíz del repo como `ChangeOrder.slnx` (no `ChangeOrder.sln`). Scripts y documentación usan ese nombre.

## Compliance / Validación

- Build CI: `.github/workflows/build.yml` se invoca contra el `.slnx`.
- Code review: cualquier PR que reintroduzca `.sln` debe ser rechazado salvo ADR nuevo que supersede.

## Referencias

- `ChangeOrder.slnx` en la raíz del repo.
- [ADR-0001](0001-onion-architecture-cqrs.md) — define los cinco proyectos `src/` listados en el `.slnx`.
