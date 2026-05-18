# ADR-0008: Workaround obligatorio HTTP/2 + IPv6 para `dotnet restore`

- **Status**: Accepted
- **Fecha**: 2026-05-12
- **Decisores**: Jose Lara
- **Tags**: tooling, environment, runbook
- **Scope**: environment-specific (máquinas de desarrollo bajo la red actual del usuario)

## Context

En el entorno de desarrollo del mantenedor, `dotnet restore` falla contra `https://api.nuget.org/v3/index.json` con el error:

```
NU1301: The SSL connection could not be established · Broken pipe
```

Comportamiento observado:

- `curl https://api.nuget.org/v3/index.json` responde **HTTP 200** correctamente desde la misma máquina y red.
- El cliente HTTP de .NET intenta **HTTP/2 con ALPN** por defecto y la red filtra ese handshake, sin degradar limpiamente a HTTP/1.1.
- Sin workaround, los comandos `dotnet restore`, `dotnet build` y `dotnet run` que requieran red se cuelgan ~60s y fallan.

Esto **no es un bug del proyecto** ni del propio .NET — es una interacción entre el cliente .NET y un filtrado de red específico del entorno. Pero al ser **reproducible y bloqueante**, debe estar documentado para que cualquier colaborador (incluyendo agentes automáticos) lo aplique sin investigar el síntoma desde cero.

## Decision

Cualquier invocación de `dotnet` que toque NuGet en este entorno **debe ejecutarse con las siguientes variables de entorno exportadas**:

```bash
export DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP2SUPPORT=false
export DOTNET_SYSTEM_NET_DISABLEIPV6=1
```

- `DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP2SUPPORT=false` — fuerza HTTP/1.1, evitando el ALPN filtrado.
- `DOTNET_SYSTEM_NET_DISABLEIPV6=1` — fuerza resolución IPv4, evitando intentos lentos contra registros AAAA.

Estas variables están documentadas en `CLAUDE.md` (sección "Gotcha de entorno — restore de NuGet") como instrucción operativa.

El status `Accepted` aplica al **scope environment-specific**: el ADR documenta un workaround operativo, no una decisión arquitectónica del software. Si el filtrado de red desaparece, el workaround puede dejar de aplicarse sin necesidad de superseder el ADR — basta marcarlo como `Deprecated`.

## Alternatives considered

- **Configurar un feed NuGet local o un mirror** — descartada por el costo operativo de mantener el espejo y la divergencia inevitable con upstream.
- **Usar VPN para evitar el filtrado** — descartada por la complejidad operativa y porque no resuelve el problema en escenarios automatizados sin VPN activa.
- **No documentar el workaround y dejar que cada colaborador lo descubra** — descartada por el costo de diagnóstico recurrente (el síntoma `NU1301 · Broken pipe` no es obvio que apunte a ALPN).

## Consequences

### Positivas

- **`dotnet` funciona consistentemente** en el entorno actual sin diagnóstico repetido.
- **Documentación clara** para futuros colaboradores y agentes automatizados.
- **Reversible sin esfuerzo** — quitar las variables vuelve al comportamiento por defecto.

### Negativas

- **Pérdida de HTTP/2** en operaciones de restore — costo de performance marginal (restore no es ruta caliente).
- **No portable** — el workaround puede ser innecesario o contraproducente en otros entornos.

### Neutras

- Las variables se exportan a nivel de shell del usuario; no se persisten en `Directory.Build.props` ni en archivos del repo (no se quieren imponer a colaboradores que no las necesiten).

## Compliance / Validación

- Si `dotnet restore` falla con `NU1301 · Broken pipe` y `curl` a la misma URL funciona, verificar que ambas variables estén exportadas en la sesión actual antes de investigar otra causa.
- Re-evaluar este ADR si el comportamiento de red cambia y `dotnet restore` funciona sin las variables.

## Referencias

- `CLAUDE.md` — sección "Gotcha de entorno — restore de NuGet".
- Documentación oficial .NET sobre runtime configuration: `DOTNET_SYSTEM_NET_*`.
