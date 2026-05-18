# ADR-0007: Publicación manual de imágenes Docker (no automatizada en CI)

- **Status**: Accepted
- **Fecha**: 2026-05-14
- **Decisores**: Jose Lara
- **Tags**: deployment, docker, ci

## Context

El proyecto publica imágenes Docker en Docker Hub bajo el repositorio `jlarapr/changeorder-api`, con tags `:<version>` y `:latest`. La pregunta operativa es **dónde** se construye y publica esa imagen.

El precedente que motivó esta decisión: el 2026-05-14 se publicó el tag `jlarapr/changeorder-api:1.0.1` con un **desfase entre el tag externo y la versión interna del binario**. La causa fue un bump semver hecho por convención (sin que el binario reflejara ese tag) y empujado a la registry pública. Restaurar la coherencia exigió rebuild + push correctivo del `1.1.0`.

Bajo CI automatizado, el riesgo de repetir este tipo de incidente aumenta: cualquier disparador defectuoso (tag mal aplicado, push accidental, release-please ejecutado fuera de tiempo) termina publicando inmediatamente una imagen pública.

## Decision

La publicación de imágenes Docker es **manual, deliberada y ejecutada desde la máquina del usuario**. **No se automatiza en CI**.

Reglas operativas:

1. Tras cada commit a `main` que afecte runtime en cualquier proyecto de `src/` (Domain / Business / Data / Presentation / Host), el usuario publica `jlarapr/changeorder-api:<version>` desde su máquina.
2. El workflow `release-please.yml` **no contiene pasos de `docker push`**.
3. El comando canónico cumple los requisitos globales de supply-chain (multi-arch + attestations):

   ```bash
   docker buildx build \
     --platform linux/amd64,linux/arm64 \
     --file src/ChangeOrder.Host/Dockerfile \
     --tag jlarapr/changeorder-api:<version> \
     --tag jlarapr/changeorder-api:latest \
     --provenance=mode=max \
     --sbom=true \
     --push \
     .
   ```

4. Para prereleases (`-rc`, `-beta`), se **omite** el tag `:latest`.
5. Tras el push se verifica el manifest con `docker buildx imagetools inspect …` esperando dos platform manifests + dos `attestation-manifest`.

## Alternatives considered

- **Publicar automáticamente en `release-please` al crear el tag** — descartada por el incidente del `:1.0.1`. La automatización amplifica errores y no introduce un humano que pueda verificar coherencia tag↔binario antes de exponer la imagen.
- **Publicar en cada push a `main`** — descartada por la misma razón, más el costo de imágenes innecesarias en la registry.
- **Pipeline manual disparado desde GitHub Actions (workflow_dispatch)** — descartada por ahora; añade infraestructura sin resolver el problema central (la verificación humana). Reconsiderar si el flujo crece más allá del mantenedor único.

## Consequences

### Positivas

- **Control humano explícito** sobre cada imagen pública.
- **Verificación obligatoria** tag↔binario antes del push.
- **Cero superficie de ataque** desde secretos de Docker Hub en el repo (no hay token de push en GitHub Actions).

### Negativas

- **Cuello de botella en una persona** — solo el mantenedor con credenciales puede publicar.
- **Disciplina manual** — olvido de push tras un release deja la registry desactualizada hasta corregirlo.
- **No hay trazabilidad automática** entre el commit y la imagen publicada — debe documentarse manualmente cuando es relevante.

### Neutras

- Las credenciales de Docker Hub residen exclusivamente en la máquina del usuario.

## Compliance / Validación

- Revisar que `release-please.yml` y cualquier otro workflow **no** contenga pasos de `docker push` ni `docker login` contra Docker Hub.
- Tras cada push, verificar el manifest con `docker buildx imagetools inspect jlarapr/changeorder-api:<version>`.

## Referencias

- Incidente `:1.0.1` del 2026-05-14 que motivó la regla.
- `CLAUDE.md` — sección "Publicación de imagen Docker".
- Política global de supply-chain en `~/.claude/CLAUDE.md` (flags `--provenance`, `--sbom`, multi-arch).
