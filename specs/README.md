# Teck.Cloud OpenAPI Specs

This directory contains exported OpenAPI specifications for all Teck.Cloud services.

## Source of truth

These files are **auto-generated** by the CI pipeline (`.github/workflows/export-openapi-specs.yml`).
Do not edit them manually.

## File naming

```
specs/
  {service-name}.openapi.json
```

Example: `basket-api.openapi.json`, `catalog-api.openapi.json`

## Consumers

- **Teck.Web** — generates TypeScript client types via `openapi-typescript`
- **Documentation** — Swagger UI / Scalar references these specs

## Regeneration

Triggered automatically on every merge to `main` in Teck.Cloud.

To regenerate locally:
1. Start the Aspire AppHost: `dotnet run --project src/aspire/Teck.Cloud.AppHost`
2. In Teck.Web, run: `bun run generate --fetch`
