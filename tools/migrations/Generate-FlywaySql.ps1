[CmdletBinding()]
param (
    [Parameter(Mandatory = $false)]
    [string[]]$ServiceName = @("catalog", "customer"),

    [Parameter(Mandatory = $false)]
    [string[]]$Providers = @("postgres"),

    [Parameter(Mandatory = $false)]
    [switch]$ChangeToSolutionDir
)

$ErrorActionPreference = "Stop"

if ($ChangeToSolutionDir -or -not $PSBoundParameters.ContainsKey("ChangeToSolutionDir")) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $solutionDir = Resolve-Path (Join-Path $scriptDir "..\..")
    Set-Location $solutionDir
    Write-Host "Working directory set to: $solutionDir" -ForegroundColor Cyan
}

$solutionRoot = (Get-Location).Path

$serviceMap = @{
    "catalog" = @{
        AppKey = "catalog-api"
        Providers = @{
            "postgres" = @{
                MigrationProject = "src/services/catalog/Catalog.Infrastructure.Migrations.PostgreSQL/Catalog.Infrastructure.Migrations.PostgreSQL.csproj"
                Context = "ApplicationWriteDbContext"
                MigrationDirectory = "src/services/catalog/Catalog.Infrastructure.Migrations.PostgreSQL/Persistence/Migrations"
                OutputDirectory = "deployment/catalog-api/database/flyway/postgres"
                ServerType = "postgres"
                ConnectionString = "Host=localhost;Database=teck_catalog;Username=postgres;Password=postgres;Search Path=public"
            }
            "sqlserver" = @{
                MigrationProject = "src/services/catalog/Catalog.Infrastructure.Migrations.SqlServer/Catalog.Infrastructure.Migrations.SqlServer.csproj"
                Context = "ApplicationWriteDbContext"
                MigrationDirectory = "src/services/catalog/Catalog.Infrastructure.Migrations.SqlServer/Persistence/Migrations"
                OutputDirectory = "deployment/catalog-api/database/flyway/sqlserver"
                ServerType = "sqlserver"
                ConnectionString = "Server=localhost;Database=Teck_catalog;User Id=sa;Password=Password123!;TrustServerCertificate=True"
            }
            "mysql" = @{
                MigrationProject = "src/services/catalog/Catalog.Infrastructure.Migrations.MySql/Catalog.Infrastructure.Migrations.MySql.csproj"
                Context = "ApplicationWriteDbContext"
                MigrationDirectory = "src/services/catalog/Catalog.Infrastructure.Migrations.MySql/Persistence/Migrations"
                OutputDirectory = "deployment/catalog-api/database/flyway/mysql"
                ServerType = "mysql"
                ConnectionString = "Server=localhost;Database=teck_catalog;User=root;Password=root"
            }
        }
    }
    "customer" = @{
        AppKey = "customer-api"
        Providers = @{
            "postgres" = @{
                MigrationProject = "src/services/customer/Customer.Infrastructure.Migrations.PostgreSQL/Customer.Infrastructure.Migrations.PostgreSQL.csproj"
                Context = "CustomerWriteDbContext"
                MigrationDirectory = "src/services/customer/Customer.Infrastructure.Migrations.PostgreSQL/Persistence/Migrations"
                OutputDirectory = "deployment/customer-api/database/flyway/postgres"
                ServerType = "postgres"
                ConnectionString = "Host=localhost;Database=teck_customer;Username=postgres;Password=postgres;Search Path=public"
            }
            "sqlserver" = @{
                MigrationProject = "src/services/customer/Customer.Infrastructure.Migrations.SqlServer/Customer.Infrastructure.Migrations.SqlServer.csproj"
                Context = "CustomerWriteDbContext"
                MigrationDirectory = "src/services/customer/Customer.Infrastructure.Migrations.SqlServer/Persistence/Migrations"
                OutputDirectory = "deployment/customer-api/database/flyway/sqlserver"
                ServerType = "sqlserver"
                ConnectionString = "Server=localhost;Database=Teck_customer;User Id=sa;Password=Password123!;TrustServerCertificate=True"
            }
            "mysql" = @{
                MigrationProject = "src/services/customer/Customer.Infrastructure.Migrations.MySql/Customer.Infrastructure.Migrations.MySql.csproj"
                Context = "CustomerWriteDbContext"
                MigrationDirectory = "src/services/customer/Customer.Infrastructure.Migrations.MySql/Persistence/Migrations"
                OutputDirectory = "deployment/customer-api/database/flyway/mysql"
                ServerType = "mysql"
                ConnectionString = "Server=localhost;Database=teck_customer;User=root;Password=root"
            }
        }
    }
}

$providerAliases = @{
    "mariadb" = "mysql"
}

$restoredProjects = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

function Get-NormalizedProvider {
    param([string]$Provider)

    $normalizedProvider = $Provider.ToLowerInvariant()
    if ($providerAliases.ContainsKey($normalizedProvider)) {
        return $providerAliases[$normalizedProvider]
    }

    return $normalizedProvider
}

function Get-MigrationDescriptors {
    param([string]$MigrationDirectory)

    if (-not (Test-Path $MigrationDirectory)) {
        throw "Migration directory was not found at '$MigrationDirectory'."
    }

    $migrationFiles = Get-ChildItem -Path $MigrationDirectory -Filter '*.Designer.cs' | Where-Object {
        $_.Name -notlike '*ModelSnapshot*'
    }

    $descriptors = foreach ($migrationFile in $migrationFiles) {
        if ($migrationFile.Name -match '^(?<Id>\d+)_(?<Name>.+)\.Designer\.cs$') {
            [PSCustomObject]@{
                Id = $matches.Id
                Name = $matches.Name
                EfMigrationName = "$($matches.Id)_$($matches.Name)"
            }
        }
    }

    return $descriptors | Sort-Object Id
}

function Get-FlywayDescription {
    param([string]$MigrationName)

    $description = $MigrationName -replace '[^A-Za-z0-9]+', '_'
    $description = $description.Trim('_')
    if ([string]::IsNullOrWhiteSpace($description)) {
        return 'migration'
    }

    return $description
}

function Ensure-ProjectRestore {
    param([string]$ProjectPath)

    if ($restoredProjects.Contains($ProjectPath)) {
        return
    }

    Write-Host "Restoring project: $ProjectPath" -ForegroundColor DarkCyan
    & dotnet restore $ProjectPath --locked-mode
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed for '$ProjectPath'."
    }

    $restoredProjects.Add($ProjectPath) | Out-Null
}

function Update-FlywayKustomization {
    param(
        [string]$SolutionRoot,
        [string]$AppKey,
        [string]$Provider,
        [string]$OutputDirectory
    )

    $relativeOutputDirectory = Resolve-Path -Relative $OutputDirectory
    $kustomizationPath = Join-Path $OutputDirectory '..\kustomization.yaml'
    $jobFileName = "job.$Provider.yaml"
    $configMapName = "$AppKey-flyway-$Provider-sql"
    $sqlFiles = Get-ChildItem -Path $OutputDirectory -Filter 'V*.sql' | Sort-Object Name

    $builder = [System.Collections.Generic.List[string]]::new()
    $builder.Add('apiVersion: kustomize.config.k8s.io/v1beta1')
    $builder.Add('kind: Kustomization')
    $builder.Add('generatorOptions:')
    $builder.Add('  immutable: true')
    $builder.Add('  labels:')
    $builder.Add("    app.kubernetes.io/name: $AppKey")
    $builder.Add("    app.kubernetes.io/component: database-migration")
    $builder.Add('resources:')
    $builder.Add("  - $jobFileName")
    $builder.Add('configMapGenerator:')
    $builder.Add("  - name: $configMapName")
    $builder.Add('    files:')

    if ($sqlFiles.Count -eq 0) {
        $builder.Add("      - .placeholder=./$Provider/.placeholder")
    }
    else {
        foreach ($sqlFile in $sqlFiles) {
            $builder.Add("      - $($sqlFile.Name)=./$Provider/$($sqlFile.Name)")
        }
    }

    $content = ($builder -join "`r`n") + "`r`n"
    Set-Content -Path $kustomizationPath -Value $content -Encoding utf8
}

& dotnet tool run dotnet-ef --version *> $null
if ($LASTEXITCODE -ne 0) {
    throw "The dotnet-ef local tool is not available. Run 'dotnet tool restore --tool-manifest .config/dotnet-tools.json' first."
}

foreach ($rawServiceName in $ServiceName) {
    $serviceKey = $rawServiceName.ToLowerInvariant()
    if (-not $serviceMap.ContainsKey($serviceKey)) {
        throw "Unsupported service '$rawServiceName'. Valid services: $($serviceMap.Keys -join ', ')."
    }

    $serviceConfig = $serviceMap[$serviceKey]

    foreach ($rawProvider in $Providers) {
        $providerKey = Get-NormalizedProvider $rawProvider
        if (-not $serviceConfig.Providers.ContainsKey($providerKey)) {
            throw "Unsupported provider '$rawProvider' for service '$rawServiceName'. Valid providers: $($serviceConfig.Providers.Keys -join ', '), mariadb."
        }

        $providerConfig = $serviceConfig.Providers[$providerKey]
        $migrationDirectory = Join-Path $solutionRoot $providerConfig.MigrationDirectory
        $outputDirectory = Join-Path $solutionRoot $providerConfig.OutputDirectory
        $migrationProject = Join-Path $solutionRoot $providerConfig.MigrationProject
        $placeholderPath = Join-Path $outputDirectory '.placeholder'
        $migrations = Get-MigrationDescriptors $migrationDirectory

        New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
        Get-ChildItem -Path $outputDirectory -Filter 'V*.sql' -ErrorAction SilentlyContinue | Remove-Item -Force
        $gitKeepPath = Join-Path $outputDirectory '.gitkeep'
        if (Test-Path $gitKeepPath) {
            Remove-Item $gitKeepPath -Force
        }

        if (-not (Test-Path $placeholderPath)) {
            Set-Content -Path $placeholderPath -Value "Generated by CI when Flyway SQL is refreshed." -Encoding utf8
        }

        if ($migrations.Count -eq 0) {
            Write-Host "No EF migrations found for $($serviceConfig.AppKey) ($providerKey)." -ForegroundColor DarkYellow
            Update-FlywayKustomization -SolutionRoot $solutionRoot -AppKey $serviceConfig.AppKey -Provider $providerKey -OutputDirectory $outputDirectory
            continue
        }

        Write-Host "Generating Flyway SQL for $($serviceConfig.AppKey) ($providerKey)" -ForegroundColor Yellow

        Ensure-ProjectRestore -ProjectPath $migrationProject

        $previousServerType = $env:MIGRATION_SERVER_TYPE
        $previousConnectionString = $env:MIGRATION_CONNECTION_STRING
        $env:MIGRATION_SERVER_TYPE = $providerConfig.ServerType
        $env:MIGRATION_CONNECTION_STRING = $providerConfig.ConnectionString

        try {
            $previousMigration = '0'

            foreach ($migration in $migrations) {
                $flywayFileName = "V$($migration.Id)__$(Get-FlywayDescription $migration.Name).sql"
                $flywayOutputPath = Join-Path $outputDirectory $flywayFileName
                $efArguments = @(
                    'tool',
                    'run',
                    'dotnet-ef',
                    'migrations',
                    'script',
                    $previousMigration,
                    $migration.EfMigrationName,
                    '--project',
                    $migrationProject,
                    '--startup-project',
                    $migrationProject,
                    '--context',
                    $providerConfig.Context,
                    '--output',
                    $flywayOutputPath
                )

                & dotnet @efArguments
                if ($LASTEXITCODE -ne 0) {
                    throw "dotnet ef migrations script failed for $($serviceConfig.AppKey) ($providerKey) migration '$($migration.EfMigrationName)'."
                }

                $previousMigration = $migration.EfMigrationName
            }

            if (Test-Path $placeholderPath) {
                Remove-Item $placeholderPath -Force
            }

            Update-FlywayKustomization -SolutionRoot $solutionRoot -AppKey $serviceConfig.AppKey -Provider $providerKey -OutputDirectory $outputDirectory
        }
        finally {
            $env:MIGRATION_SERVER_TYPE = $previousServerType
            $env:MIGRATION_CONNECTION_STRING = $previousConnectionString
        }
    }
}