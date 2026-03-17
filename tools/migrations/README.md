# Database Migration Scripts

This folder contains scripts for EF Core migrations in Teck.Cloud using **provider-specific migration projects**.

Each service has one migration project per provider, and each provider project contains a single migration stream for the write model.

## Project layout

Catalog:
- src/services/catalog/Catalog.Infrastructure.Migrations.PostgreSQL
- src/services/catalog/Catalog.Infrastructure.Migrations.SqlServer
- src/services/catalog/Catalog.Infrastructure.Migrations.MySql

Customer:
- src/services/customer/Customer.Infrastructure.Migrations.PostgreSQL
- src/services/customer/Customer.Infrastructure.Migrations.SqlServer
- src/services/customer/Customer.Infrastructure.Migrations.MySql

Inside each migration project:
- Persistence/Migrations

## Supported services

- catalog
- customer

## Supported providers

- postgres
- sqlserver
- mysql

## Flyway SQL generation

Flyway SQL lives under the deployment app folders so GitOps overlays can consume service-owned migration directories:

```text
deployment/
	catalog-api/
		database/
			flyway/
					kustomization.yaml
					job.postgres.yaml
				postgres/
				sqlserver/
				mysql/
	customer-api/
		database/
			flyway/
				kustomization.yaml
				job.postgres.yaml
				postgres/
				sqlserver/
				mysql/
```

The preferred path is the GitHub Actions workflow at `.github/workflows/flyway-sql-sync.yaml`. For same-repository pull requests it regenerates the Flyway SQL and commits the refreshed files back to the PR branch automatically so Argo preview generation can pick them up from the branch.

Generate or refresh the Flyway SQL manually when you need to troubleshoot the pipeline or bootstrap locally:

```powershell
.\tools\migrations\Generate-FlywaySql.ps1
```

Generate SQL for a specific service and provider:

```powershell
.\tools\migrations\Generate-FlywaySql.ps1 -ServiceName catalog -Providers @('postgres')
```

The Flyway generator does not need a live database. It uses the provider-specific EF Core migration project and emits one Flyway versioned SQL file per EF migration.

For PostgreSQL, the generator also refreshes `deployment/<app>/database/flyway/kustomization.yaml` so Kustomize can generate a per-service ConfigMap from the checked-in SQL files. The matching `job.postgres.yaml` mounts that generated ConfigMap into the official Flyway container.

Provider support:

- `postgres`
- `sqlserver`
- `mysql`
- `mariadb` maps to the `mysql` provider folder

Prerequisites:

- Run `dotnet tool restore --tool-manifest .config/dotnet-tools.json`.
- Ensure the targeted migration projects build successfully.

Behavior in CI:

- Same-repository PRs: generate Flyway SQL and commit any refreshed files back to the PR branch automatically.
- Pushes to protected branches and PRs from forks: validate drift only and fail instead of attempting a write-back.
- The current workflow generates PostgreSQL artifacts first while keeping the layout ready for SQL Server and MySQL.

Secret contract for the base PostgreSQL jobs:

- `catalog-api-flyway-postgres` with keys `url`, `user`, and `password`
- `customer-api-flyway-postgres` with keys `url`, `user`, and `password`

These names are defaults for the base manifests in this repository. The central GitOps repository can patch them per environment.

## Scripts

### Add a migration

```powershell
.\tools\migrations\Add-Migration.ps1 -ServiceName catalog -MigrationName Initial
```

Specific providers:

```powershell
.\tools\migrations\Add-Migration.ps1 -ServiceName customer -MigrationName AddTenantMetadata -Providers @('postgres','sqlserver')
```

### Remove latest migration

```powershell
.\tools\migrations\Remove-Migration.ps1 -ServiceName customer -Providers @('postgres')
```

## Auto-create behavior

If a provider migration project is missing, Add/Remove scripts will:
- create the migration project,
- add a ProjectReference to the service Infrastructure project,
- create Persistence/Migrations,
- add the project to Teck.Cloud.slnx.

## Compatibility wrappers

- Add-Migration-PerService.ps1 delegates to Add-Migration.ps1
- Remove-Migration-PerService.ps1 delegates to Remove-Migration.ps1

## Notes

- Run scripts from solution root (default behavior in scripts also switches to root).
- Run `dotnet tool restore --tool-manifest .config/dotnet-tools.json` before using the Flyway generator.
- Ensure service startup projects build successfully before migration commands.
