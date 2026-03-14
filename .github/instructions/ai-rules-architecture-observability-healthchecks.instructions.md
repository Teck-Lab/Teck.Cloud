---
description: 'Converted from docs/ai/rules/architecture/observability-healthchecks.md'
applyTo: 'src/**/*.cs,tests/**/*.cs'
---
# Observability and Health Checks

## Intent

Keep OpenTelemetry and health-check registration consistent across services while minimizing duplication.

## Ownership Model

- Central defaults belong in shared infrastructure (`ServiceDefaults` + shared observability extensions).
- Module-owned dependencies (database, cache, message bus, identity) register module-specific checks through shared helper extensions.
- Avoid per-service bespoke health-check wiring when a shared helper exists.

## OpenTelemetry

- Keep exporter/resource/process/HTTP/runtime baseline in shared observability extensions.
- Module-specific instrumentation is allowed only where a module owns lifecycle details.
- Redis tracing must use the same `ConnectionMultiplexer` instance as caching/locking when caching is enabled.

## Health Check Conventions

- Use consistent names and tags:
  - Database: `database`, provider (`postgres`/`mysql`/`sqlserver`), role (`write`/`read`)
  - Message bus: `messagebus`, `rabbitmq`
  - Cache: `cache`, `redis`
  - Identity: `identity`, `keycloak`, `openid`
- Keep liveness lightweight (`live` self-check) and dependency checks in readiness.
- If read and write DB endpoints are identical, register only the write check.

## Placement

- Shared health helper extensions: `src/buildingblocks/SharedKernel.Infrastructure/HealthChecks/*`
- Service/module composition: service infrastructure extension classes in each module
- Endpoint mapping (`/health`, `/alive`): service defaults