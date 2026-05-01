# Add-Migration.ps1
# Generate EF Core migrations into provider-specific migration projects per service.
# Each provider project contains a single write migration stream under Persistence/Migrations.

param (
    [Parameter(Mandatory = $true)]
    [string]$ServiceName,

    [Parameter(Mandatory = $true)]
    [string]$MigrationName,

    [Parameter(Mandatory = $false)]
    [string]$OutputDir = $null,

    [Parameter(Mandatory = $false)]
    [switch]$ChangeToSolutionDir,

    [Parameter(Mandatory = $false)]
    [string[]]$Providers = @("postgres", "sqlserver", "mysql")
)

if ($ChangeToSolutionDir -or -not $PSBoundParameters.ContainsKey('ChangeToSolutionDir')) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $solutionDir = Resolve-Path (Join-Path $scriptDir "..\..")
    Set-Location $solutionDir
    Write-Host "Working directory set to: $solutionDir" -ForegroundColor Cyan
}

$solutionFile = "Teck.Cloud.slnx"

$providerConfigs = @{
    "postgres" = @{
        DisplayName = "PostgreSQL"
        FolderSuffix = "PostgreSQL"
        ServerType = "postgres"
        DefaultConnectionString = "Host=localhost;Database=teck_migrations;Username=postgres;Password=postgres"
    }
    "sqlserver" = @{
        DisplayName = "SQL Server"
        FolderSuffix = "SqlServer"
        ServerType = "sqlserver"
        DefaultConnectionString = "Server=(localdb)\mssqllocaldb;Database=TeckCloudMigrations;Trusted_Connection=true;MultipleActiveResultSets=true;"
    }
    "mysql" = @{
        DisplayName = "MySQL"
        FolderSuffix = "MySql"
        ServerType = "mysql"
        DefaultConnectionString = "Server=localhost;Database=teck_migrations;User=root;Password=root;"
    }
}

$serviceMap = @{
    "basket" = @{
        StartupProject = "src/services/basket/Basket.Infrastructure/Basket.Infrastructure.csproj"
        InfrastructureProject = "src/services/basket/Basket.Infrastructure/Basket.Infrastructure.csproj"
        WriteContextType = "Basket.Infrastructure.Persistence.BasketPersistenceDbContext"
        MigrationProjectPrefix = "src/services/basket/Basket.Infrastructure.Migrations"
    }
    "catalog" = @{
        StartupProject = "src/services/catalog/Catalog.Infrastructure/Catalog.Infrastructure.csproj"
        InfrastructureProject = "src/services/catalog/Catalog.Infrastructure/Catalog.Infrastructure.csproj"
        WriteContextType = "Catalog.Infrastructure.Persistence.ApplicationWriteDbContext"
        MigrationProjectPrefix = "src/services/catalog/Catalog.Infrastructure.Migrations"
    }
    "customer" = @{
        StartupProject = "src/services/customer/Customer.Infrastructure/Customer.Infrastructure.csproj"
        InfrastructureProject = "src/services/customer/Customer.Infrastructure/Customer.Infrastructure.csproj"
        WriteContextType = "Customer.Infrastructure.Persistence.CustomerWriteDbContext"
        MigrationProjectPrefix = "src/services/customer/Customer.Infrastructure.Migrations"
    }
    "order" = @{
        StartupProject = "src/services/order/Order.Infrastructure/Order.Infrastructure.csproj"
        InfrastructureProject = "src/services/order/Order.Infrastructure/Order.Infrastructure.csproj"
        WriteContextType = "Order.Infrastructure.Persistence.OrderPersistenceDbContext"
        MigrationProjectPrefix = "src/services/order/Order.Infrastructure.Migrations"
    }
}

function Ensure-MigrationProject {
    param(
        [hashtable]$Service,
        [hashtable]$ProviderConfig
    )

    $projectPath = "$($Service.MigrationProjectPrefix).$($ProviderConfig.FolderSuffix)/$([IO.Path]::GetFileName("$($Service.MigrationProjectPrefix).$($ProviderConfig.FolderSuffix)"))" + ".csproj"

    $contextType = $Service.WriteContextType
    $contextNamespace = $contextType.Substring(0, $contextType.LastIndexOf('.'))
    $contextTypeName = $contextType.Substring($contextType.LastIndexOf('.') + 1)

    function Ensure-DesignTimeFactory {
        param(
            [string]$ProjectDir,
            [string]$ContextNamespace,
            [string]$ContextTypeName,
            [hashtable]$ProviderConfig
        )

        $factoryDir = Join-Path $ProjectDir "DesignTime"
        New-Item -ItemType Directory -Path $factoryDir -Force | Out-Null
        $legacyFactoryPath = Join-Path $factoryDir "$($ContextTypeName)DesignTimeFactory.cs"
        $factoryPath = Join-Path $factoryDir "$($ContextTypeName)DesignTimeFactory.g.cs"
        Remove-Item $legacyFactoryPath -ErrorAction SilentlyContinue

        $defaultConnection = ($ProviderConfig.DefaultConnectionString -replace '\\', '\\\\').Replace('"', '`"')
        $migrationAssemblyName = [IO.Path]::GetFileName($ProjectDir)
        $providerOptionsLine = switch ($ProviderConfig.ServerType) {
            'postgres' { "optionsBuilder.UseNpgsql(connectionString, b => b.MigrationsAssembly(`"$migrationAssemblyName`"));" }
            'sqlserver' { "optionsBuilder.UseSqlServer(connectionString, b => b.MigrationsAssembly(`"$migrationAssemblyName`"));" }
            default { "optionsBuilder.UseMySQL(connectionString, b => b.MigrationsAssembly(`"$migrationAssemblyName`"));" }
        }

        $factoryContent = @"
    // <auto-generated />
    #pragma warning disable SA1633,SA1515,CS1591

using $ContextNamespace;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MigrationProject.DesignTime;

public sealed class $($ContextTypeName)DesignTimeFactory : IDesignTimeDbContextFactory<$ContextTypeName>
{
    public $ContextTypeName CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<$ContextTypeName>();
        var connectionString = Environment.GetEnvironmentVariable("MIGRATION_CONNECTION_STRING") ?? "$defaultConnection";
        $providerOptionsLine

        return new $ContextTypeName(optionsBuilder.Options);
    }
}
"@

    Set-Content -Path $factoryPath -Value $factoryContent
    }

    if (Test-Path $projectPath) {
        $projectDir = Split-Path $projectPath -Parent
        Ensure-DesignTimeFactory -ProjectDir $projectDir -ContextNamespace $contextNamespace -ContextTypeName $contextTypeName -ProviderConfig $ProviderConfig
        return $projectPath
    }

    $projectDir = Split-Path $projectPath -Parent
    New-Item -ItemType Directory -Path $projectDir -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $projectDir "Persistence\Migrations") -Force | Out-Null

    $infraAbsolute = (Resolve-Path $Service.InfrastructureProject).Path
    $projectAbsoluteDir = (Resolve-Path $projectDir).Path
    $relativeInfrastructurePath = [IO.Path]::GetRelativePath($projectAbsoluteDir, $infraAbsolute).Replace('/', '\\')

    $projectContent = @"
<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
        <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
        <NoWarn>$(NoWarn);S1186</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""Microsoft.EntityFrameworkCore.Design"">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include=""$relativeInfrastructurePath"" />
  </ItemGroup>

</Project>
"@

    Set-Content -Path $projectPath -Value $projectContent -NoNewline
    Write-Host "Created migration project: $projectPath" -ForegroundColor Green

    Ensure-DesignTimeFactory -ProjectDir $projectDir -ContextNamespace $contextNamespace -ContextTypeName $contextTypeName -ProviderConfig $ProviderConfig

    if (Test-Path $solutionFile) {
        $solutionContent = Get-Content $solutionFile -Raw
        if ($solutionContent -notmatch [Regex]::Escape($projectPath)) {
            dotnet sln $solutionFile add $projectPath | Out-Null
            Write-Host "Added project to solution: $projectPath" -ForegroundColor Green
        }
    }

    return $projectPath
}

$serviceKey = $ServiceName.ToLowerInvariant()
if (-not $serviceMap.ContainsKey($serviceKey)) {
    Write-Error "Service '$ServiceName' is not supported. Valid services are: $($serviceMap.Keys -join ', ')"
    exit 1
}

$service = $serviceMap[$serviceKey]
$contextType = $service.WriteContextType

Write-Host "=== Multi-Provider Migration Generation (Separate Projects) ===" -ForegroundColor Magenta
Write-Host "Service: $ServiceName" -ForegroundColor Cyan
Write-Host "Migration: $MigrationName" -ForegroundColor Cyan
Write-Host "Providers: $($Providers -join ', ')" -ForegroundColor Cyan
Write-Host "Startup project: $($service.StartupProject)" -ForegroundColor Gray
Write-Host "DbContext: $contextType" -ForegroundColor Gray
Write-Host "" -ForegroundColor Gray

$successfulMigrations = @()
$failedMigrations = @()

foreach ($provider in $Providers) {
    if (-not $providerConfigs.ContainsKey($provider)) {
        Write-Warning "Unknown provider '$provider'. Skipping. Valid providers are: $($providerConfigs.Keys -join ', ')"
        continue
    }

    $providerConfig = $providerConfigs[$provider]
    $providerDisplayName = $providerConfig.DisplayName
    $providerMigrationName = "$MigrationName$($providerConfig.FolderSuffix)"

    Write-Host "--- Generating migration for $providerDisplayName ---" -ForegroundColor Yellow

    $migrationProject = Ensure-MigrationProject -Service $service -ProviderConfig $providerConfig
    $migrationProjectDir = Split-Path $migrationProject -Parent

    $env:MIGRATION_DB_PROVIDER = $provider
    $env:MIGRATION_CONNECTION_STRING = $providerConfig.DefaultConnectionString
    $env:MIGRATION_SERVER_TYPE = $providerConfig.ServerType
    Remove-Item env:MIGRATION_ASSEMBLY -ErrorAction SilentlyContinue

    $providerOutputDir = if ([string]::IsNullOrWhiteSpace($OutputDir)) {
        "Persistence\\Migrations"
    }
    else {
        $OutputDir
    }

    $fullOutputDir = Join-Path $migrationProjectDir $providerOutputDir
    if (-not (Test-Path $fullOutputDir)) {
        New-Item -ItemType Directory -Path $fullOutputDir -Force | Out-Null
    }

    $dotnetEfCommand = "dotnet ef migrations add $providerMigrationName " +
        "--startup-project `"$migrationProject`" " +
        "--project `"$migrationProject`" " +
        "--context $contextType " +
        "--output-dir `"$providerOutputDir`""

    Write-Host "Executing: $dotnetEfCommand" -ForegroundColor Gray
    Invoke-Expression $dotnetEfCommand

    if ($LASTEXITCODE -eq 0) {
        if (Test-Path $fullOutputDir) {
            $migrationFiles = Get-ChildItem -Path $fullOutputDir -Filter "*.cs" -Recurse | Where-Object { $_.Name -like "*$providerMigrationName*" }
            foreach ($file in $migrationFiles) {
                $content = Get-Content $file.FullName -Raw
                $content = $content -replace ': Migration\s*{', ': global::Microsoft.EntityFrameworkCore.Migrations.Migration {'
                $content = $content -replace 'public partial class (.+) : Migration', 'public partial class $1 : global::Microsoft.EntityFrameworkCore.Migrations.Migration'
                Set-Content -Path $file.FullName -Value $content -NoNewline
            }
        }

        Write-Host "✅ $providerDisplayName migration created successfully" -ForegroundColor Green
        $successfulMigrations += "$providerDisplayName ($migrationProject)"
    }
    else {
        Write-Host "❌ Failed to create $providerDisplayName migration" -ForegroundColor Red
        $failedMigrations += "$providerDisplayName"
    }

    Remove-Item env:MIGRATION_DB_PROVIDER -ErrorAction SilentlyContinue
    Remove-Item env:MIGRATION_CONNECTION_STRING -ErrorAction SilentlyContinue
    Remove-Item env:MIGRATION_SERVER_TYPE -ErrorAction SilentlyContinue

    Write-Host "" -ForegroundColor Gray
}

Write-Host "=== Migration Generation Summary ===" -ForegroundColor Magenta
if ($successfulMigrations.Count -gt 0) {
    Write-Host "✅ Successful migrations:" -ForegroundColor Green
    $successfulMigrations | ForEach-Object { Write-Host "   - $_" -ForegroundColor Green }
}

if ($failedMigrations.Count -gt 0) {
    Write-Host "❌ Failed migrations:" -ForegroundColor Red
    $failedMigrations | ForEach-Object { Write-Host "   - $_" -ForegroundColor Red }
}

Write-Host "`n💡 Examples:" -ForegroundColor Cyan
Write-Host ".\Add-Migration.ps1 -ServiceName customer -MigrationName Initial" -ForegroundColor Gray
Write-Host ".\Add-Migration.ps1 -ServiceName basket -MigrationName InitialBasketDrafts" -ForegroundColor Gray
Write-Host ".\Add-Migration.ps1 -ServiceName order -MigrationName InitialOrderDrafts" -ForegroundColor Gray
Write-Host ".\Add-Migration.ps1 -ServiceName catalog -MigrationName AddTenantLookup -Providers @('postgres')" -ForegroundColor Gray
Write-Host "Project layout: <Service>.Infrastructure.Migrations.<Provider>/Persistence\\Migrations" -ForegroundColor Gray

if ($failedMigrations.Count -gt 0) {
    exit 1
}

exit 0
