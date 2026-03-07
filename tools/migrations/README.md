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
- Ensure dotnet-ef is installed globally.
- Ensure service startup projects build successfully before migration commands.
