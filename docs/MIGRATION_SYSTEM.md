# Multi-Tenant Database Migration System

This document describes the database migration system for the Teck.Cloud multi-tenant application.

## Overview

The migration system handles database schema updates for:
- **Shared Database** - PostgreSQL database used by tenants on the shared tier
- **Dedicated Databases** - Tenant-specific databases (PostgreSQL or SQL Server)
- **External Databases** - Customer-managed databases

## Architecture Components

### 1. Secrets Management (`SharedKernel.Secrets`)

HashiCorp Vault integration for secure credential storage.

#### Key Files
- `VaultSecretsManager.cs` - Main Vault client implementation
- `DatabaseCredentials.cs` - Credential models with admin/app user separation
- `VaultOptions.cs` - Configuration options

#### Configuration

Add to `appsettings.json`:

```json
{
  "Vault": {
    "Address": "https://vault.example.com:8200",
    "AuthMethod": "AppRole",  // Token, AppRole, or Kubernetes
    "RoleId": "your-role-id",
    "SecretId": "your-secret-id",
    "MountPoint": "secret",
    "DatabaseSecretsPath": "database",
    "CacheDurationMinutes": 5
  }
}
```

#### Vault Secret Structure

Store credentials at these paths:

**Shared Database:**
```
secret/data/database/shared
{
  "admin_username": "postgres_admin",
  "admin_password": "admin_password",
  "app_username": "app_user",
  "app_password": "app_password",
  "host": "shared-db.example.com",
  "port": "5432",
  "database": "teck_shared"
}
```

**Tenant Database:**
```
secret/data/database/{tenantId}
{
  "admin_username": "tenant_admin",
  "admin_password": "admin_password",
  "app_username": "tenant_app",
  "app_password": "app_password",
  "host": "tenant-db.example.com",
  "port": "5432",
  "database": "tenant_db"
}
```

### 2. Migration Services (`SharedKernel.Persistence.Database.Migrations`)

Core migration orchestration logic.

#### Key Files
- `IMigrationService.cs` - Migration service interface
- `MultiTenantMigrationService.cs` - Multi-tenant migration orchestrator
- `EFCoreMigrationRunner.cs` - EF Core migration runner
- `MigrationServiceExtensions.cs` - Registration extensions

#### Features

- **Admin Credential Swapping** - Uses admin credentials from Vault for migrations
- **Runtime Credentials** - Services use app-level credentials at runtime
- **Multi-Provider Support** - PostgreSQL, SQL Server, MySQL
- **Health Checks** - Migration status verification
- **Caching** - Credentials cached for performance

### 3. Event-Driven Migrations

#### Integration Event

`TenantDatabaseProvisionedIntegrationEvent` triggers migrations when a new tenant database is provisioned.

#### Wolverine Handler

`TenantDatabaseProvisionedHandler` automatically runs migrations when the event is published.

## Usage

### 1. Service Registration

In your service's `InfrastructureServiceExtensions.cs`:

```csharp
public static IServiceCollection AddInfrastructureServices(
    this IHostApplicationBuilder builder)
{
    // Add Vault secrets management
    builder.Services.AddVaultSecretsManagement(builder.Configuration);

    // Add multi-tenant migration services
    builder.Services.AddMultiTenantMigrations<ApplicationWriteDbContext>(
        DatabaseProvider.PostgreSQL);

    return builder.Services;
}
```

### 2. Startup Migrations

#### Option A: Migrate All Databases

In `Program.cs` after `app.Build()`:

```csharp
var app = builder.Build();

// Migrate all databases on startup
if (builder.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    var results = await app.Services.MigrateAllDatabasesOnStartupAsync();
    
    foreach (var result in results)
    {
        if (!result.Success)
        {
            Log.Error("Migration failed for {TenantId}: {Error}", 
                result.TenantId ?? "shared", 
                result.ErrorMessage);
        }
    }
}
```

#### Option B: Migrate Shared Database Only

```csharp
// Migrate shared database only
if (builder.Configuration.GetValue<bool>("Database:MigrateSharedOnStartup"))
{
    var result = await app.Services.MigrateSharedDatabaseOnStartupAsync();
    
    if (!result.Success)
    {
        throw new InvalidOperationException(
            $"Shared database migration failed: {result.ErrorMessage}");
    }
}
```

### 3. Event-Driven Migrations

When a new tenant is created, publish the integration event:

```csharp
// In your tenant provisioning code
await _messageBus.PublishAsync(new TenantDatabaseProvisionedIntegrationEvent
{
    TenantId = tenant.Id,
    DatabaseStrategy = "Dedicated",
    DatabaseProvider = "PostgreSQL",
    DatabaseCreated = true
});
```

The `TenantDatabaseProvisionedHandler` will automatically:
1. Detect the event
2. Fetch admin credentials from Vault
3. Run migrations against the tenant's database
4. Log results

### 4. Manual Migration Trigger

```csharp
public class MigrationController : ControllerBase
{
    private readonly IMigrationService _migrationService;

    [HttpPost("migrate/{tenantId}")]
    public async Task<IActionResult> MigrateTenant(string tenantId)
    {
        var result = await _migrationService.MigrateTenantDatabaseAsync(tenantId);
        
        return result.Success 
            ? Ok(result) 
            : StatusCode(500, result);
    }
}
```

## Security Model

### Credential Separation

- **Admin User** - Used ONLY for migrations (DDL operations)
  - CREATE TABLE, ALTER TABLE, DROP TABLE
  - CREATE INDEX, etc.
  
- **App User** - Used by the application at runtime
  - SELECT, INSERT, UPDATE, DELETE
  - No DDL permissions

### Vault Integration

1. **Development**: Use Token auth with local Vault
2. **Kubernetes**: Use Kubernetes auth with service account
3. **Production**: Use AppRole with secret rotation

### Credential Rotation

Vault TTL settings handle automatic credential rotation:

```hcl
path "secret/data/database/*" {
  capabilities = ["read"]
  max_ttl = "24h"
  ttl = "1h"
}
```

## Configuration

### appsettings.json

```json
{
  "Database": {
    "MigrateOnStartup": false,
    "MigrateSharedOnStartup": true,
    "ConnectionTimeout": 30
  },
  "Vault": {
    "Address": "https://vault.example.com:8200",
    "AuthMethod": "Kubernetes",
    "KubernetesRole": "teck-cloud-catalog",
    "MountPoint": "secret",
    "DatabaseSecretsPath": "database",
    "CacheDurationMinutes": 5,
    "EnableTokenRenewal": true
  }
}
```

### Environment Variables

```bash
# Vault configuration
VAULT__ADDRESS=https://vault.example.com:8200
VAULT__AUTHMETHOD=AppRole
VAULT__ROLEID=your-role-id
VAULT__SECRETID=your-secret-id

# Migration settings
DATABASE__MIGRATEONSTARTUP=false
DATABASE__MIGRATESHAREDONSTARTUP=true
```

## Deployment Workflow

### 1. Initial Deployment

```bash
# 1. Deploy Vault and configure secrets
vault kv put secret/database/shared \
  admin_username=postgres_admin \
  admin_password=secure_password \
  app_username=app_user \
  app_password=app_password \
  host=shared-db.internal \
  port=5432 \
  database=teck_shared

# 2. Deploy service with MigrateSharedOnStartup=true
# The service will automatically migrate the shared database

# 3. For dedicated tenants, publish the provisioning event
```

### 2. Adding a New Tenant

```bash
# 1. Provision database infrastructure (RDS, Azure SQL, etc.)

# 2. Store credentials in Vault
vault kv put secret/database/{tenantId} \
  admin_username=tenant_admin \
  admin_password=secure_password \
  app_username=tenant_app \
  app_password=app_password \
  host=tenant-db.internal \
  port=5432 \
  database=tenant_database

# 3. Publish TenantDatabaseProvisionedIntegrationEvent
# OR manually trigger migration via API
curl -X POST https://api.teck.cloud/admin/migrate/{tenantId}
```

### 3. Schema Updates

```bash
# 1. Create EF Core migration
dotnet ef migrations add AddNewFeature

# 2. Deploy updated service
# Migrations run automatically on startup (if configured)
# OR publish event to trigger migrations for all tenants
```

## Monitoring

### Logs

Migrations generate structured logs:

```
[INF] Starting migration for tenant abc123
[INF] Found 3 pending migrations for tenant abc123
[INF] Successfully applied 3 migrations for tenant abc123 in 1234ms
```

### Health Checks

Add migration health checks:

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseMigrationHealthCheck>("database-migrations");
```

## Troubleshooting

### Migration Failed

1. Check Vault connectivity and credentials
2. Verify admin user has DDL permissions
3. Check database connectivity from the service
4. Review migration logs for specific errors

### Credential Access Denied

1. Verify Vault token/role permissions
2. Check Vault policy allows reading `secret/data/database/*`
3. Ensure credentials are stored at correct path

### Timeout Issues

1. Increase `TimeoutSeconds` in VaultOptions
2. Check network connectivity to Vault
3. Verify database connection limits

## Best Practices

1. **Always use Vault** - Never store credentials in appsettings.json in production
2. **Separate credentials** - Admin vs app user for security
3. **Test migrations** - Run migrations in staging first
4. **Monitor failures** - Set up alerts for failed migrations
5. **Rotate credentials** - Use Vault TTL for automatic rotation
6. **Backup databases** - Before running migrations in production
7. **Use transactions** - EF Core migrations are transactional by default

## Future Enhancements

- [ ] Blue/green migration strategy
- [ ] Migration rollback support
- [ ] Multi-region database support
- [ ] Automated backup before migration
- [ ] Migration verification tests
- [ ] Prometheus metrics for migration tracking
