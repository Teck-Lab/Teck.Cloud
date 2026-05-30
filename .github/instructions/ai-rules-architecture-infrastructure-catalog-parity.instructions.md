---
description: 'Catalog Infrastructure parity baseline for service infrastructure and migration wiring'
applyTo: 'src/services/**/DependencyInjection/InfrastructureServiceExtensions.cs,src/services/**/Program.cs,src/aspire/Teck.Cloud.AppHost/Program.cs,src/migrations/Teck.Cloud.Migrations/Program.cs,tools/migrations/*.ps1'
---
# Catalog Infrastructure Parity

## Intent

Keep service infrastructure setup consistent with the Catalog/Customer production pattern.

## Rules

- Use a Catalog-style infrastructure entrypoint:
  - `AddInfrastructureServices(this WebApplicationBuilder builder, Assembly applicationAssembly)`
  - `UseInfrastructureServices(this IApplicationBuilder app)`
- Validate inputs in infrastructure entrypoints.
- Resolve database provider with `configuration.GetDatabaseProvider()`.
- Resolve provider-specific migrations assembly with naming convention:
  - `<Service>.Infrastructure.Migrations.PostgreSQL`
  - `<Service>.Infrastructure.Migrations.SqlServer`
  - `<Service>.Infrastructure.Migrations.MySql`
- Configure DbContext options with provider-aware migration assembly.
- Do not use runtime schema creation for persistent services:
  - no `EnsureCreated` in API startup hosted services for DB schema management.
- Run schema changes through migration projects and migration tooling only.
- In Aspire AppHost, every DB-backed API must wait for its migration job completion before startup.
- In migration runner and migration scripts, DB-backed services must be listed explicitly.

## DB-backed vs non-DB services

- DB-backed services must implement migration project + migration runner wiring.
- Non-DB services must still follow the same infrastructure extension shape, but should not add fake DbContext or migration projects.

## Migration workflow

- Use scripts under `tools/migrations` for add/remove migration operations.
- Keep provider-specific migration streams in provider-specific migration projects.
- Keep migration project `DesignTime` factories in sync with context and migrations assembly naming.

## Verification checklist

- Service infrastructure extension signature matches Catalog style.
- App startup calls `builder.AddInfrastructureServices(applicationAssembly)`.
- DB-backed services have migration project(s) and AppHost migration orchestration.
- Migration runner supports every DB-backed service listed in AppHost.
- No direct runtime DDL bootstrap code is used for schema evolution.
