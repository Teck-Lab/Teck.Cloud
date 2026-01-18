# Catalog SQL Migration Scripts

The CI/CD pipeline publishes **provider-specific SQL scripts** for Catalog migrations into this folder.

```
Persistence/Scripts/
    PostgreSQL/   # *.sql scripts for Postgres
    SqlServer/    # *.sql scripts for SQL Server
    MySql/        # *.sql scripts for MySQL / MariaDB
```

Name scripts with a sortable prefix (e.g., `0001_Initial.sql`, `0002_AddProducts.sql`).
They are embedded into the `Catalog.Infrastructure` assembly and executed by the
`Catalog.Migration` service (DbUp).  Only place `.sql` artifacts here—generated
C# EF migration files remain under `Persistence/Migrations`.
