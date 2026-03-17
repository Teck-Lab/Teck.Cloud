# Catalog SQL Migration Scripts

This folder is no longer the deployment source for generated migration SQL.

```
deployment/catalog-api/database/flyway/
    postgres/   # versioned Flyway SQL for Postgres
    sqlserver/  # reserved for future SQL Server output
    mysql/      # reserved for future MySQL / MariaDB output
```

The CI/CD pipeline generates provider-specific Flyway SQL from the EF Core migration projects and stores the checked-in artifacts under `deployment/catalog-api/database/flyway`.

Keep EF migration source files under `Persistence/Migrations`. Do not add deployment SQL artifacts to `Persistence/Scripts`.
