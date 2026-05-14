# syntax=docker/dockerfile:1.7
# Multi-stage Dockerfile for ChangeOrder.Host.
# - Build stage: full SDK to restore + publish.
# - Runtime stage: ASP.NET runtime only, non-root user, port 8080.
# Build context expected: repository root (see .dockerignore at repo root if added).

ARG BUILD_CONFIGURATION=Release

# ─────────────────────────────────────────────────────────────
# Stage 1 — build
# ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION
WORKDIR /src

# Copy MSBuild defaults + project manifests first so `dotnet restore`
# can be cached on layers that change only when csprojs change.
COPY Directory.Build.props ./
COPY ChangeOrder.slnx ./
COPY src/ChangeOrder.Domain/ChangeOrder.Domain.csproj         src/ChangeOrder.Domain/
COPY src/ChangeOrder.Business/ChangeOrder.Business.csproj     src/ChangeOrder.Business/
COPY src/ChangeOrder.Data/ChangeOrder.Data.csproj             src/ChangeOrder.Data/
COPY src/ChangeOrder.Presentation/ChangeOrder.Presentation.csproj src/ChangeOrder.Presentation/
COPY src/ChangeOrder.Host/ChangeOrder.Host.csproj             src/ChangeOrder.Host/

RUN dotnet restore src/ChangeOrder.Host/ChangeOrder.Host.csproj

# Copy the rest of the sources and publish.
COPY src/ src/
RUN dotnet publish src/ChangeOrder.Host/ChangeOrder.Host.csproj \
    --configuration ${BUILD_CONFIGURATION} \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# ─────────────────────────────────────────────────────────────
# Stage 2 — runtime
# ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Drop privileges to the prebuilt non-root user from the runtime image.
USER $APP_UID

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ChangeOrder.Host.dll"]
