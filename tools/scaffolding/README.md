# Service Scaffolding

Use `tools/scaffolding/New-Service.ps1` to scaffold a new service with the standard layers:

- `<Service>.Api`
- `<Service>.Application`
- `<Service>.Domain`
- `<Service>.Infrastructure`

Migrations are placed in the consolidated provider projects under `src/migrations/`:
- `Teck.Cloud.Migrations.PostgreSQL/<Service>/`
- `Teck.Cloud.Migrations.SqlServer/<Service>/`
- `Teck.Cloud.Migrations.MySql/<Service>/`

## Usage

```powershell
./tools/scaffolding/New-Service.ps1 -ServiceName inventory
```

## Parameters

- `-ServiceName` (required): service slug or name, e.g. `inventory` or `price-engine`.
- `-AddToSolution` (optional, default `true`): add generated projects to `Teck.Cloud.slnx`.
- `-CreateMigrations` (optional, default `true`): include provider-specific migration projects and design-time factories.
- `-AutoWire` (optional, default `true`): wire generated service into AppHost, migration runner, and migration scripts.
- `-Force` (optional): overwrite existing files.
- `-DryRun` (optional): show actions without writing files.

## Example dry run

```powershell
./tools/scaffolding/New-Service.ps1 -ServiceName inventory -DryRun
```

## Notes

By default, the script also wires the new service into:

- AppHost registration (`src/aspire/Teck.Cloud.AppHost/Program.cs` and `Teck.Cloud.AppHost.csproj`)
- Migration runner service dispatch (`src/migrations/Teck.Cloud.Migrations/Program.cs` and `Teck.Cloud.Migrations.csproj`)
- Migration tool service maps (`tools/migrations/Add-Migration.ps1`, `tools/migrations/Remove-Migration.ps1`)

If you only want project scaffolding without these updates, run with `-AutoWire:$false`.
