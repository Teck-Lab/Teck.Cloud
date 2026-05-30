# New-Service.ps1
# Scaffolds a new service with Api, Application, Domain, Infrastructure,
# Scaffolds a new service with Api, Application, Domain, Infrastructure,
# and consolidated migration subfolders under Teck.Cloud.Migrations.*.

param (
    [Parameter(Mandatory = $true)]
    [string]$ServiceName,

    [Parameter(Mandatory = $false)]
    [bool]$AddToSolution = $true,

    [Parameter(Mandatory = $false)]
    [bool]$CreateMigrations = $true,

    [Parameter(Mandatory = $false)]
    [bool]$AutoWire = $true,

    [Parameter(Mandatory = $false)]
    [switch]$Force,

    [Parameter(Mandatory = $false)]
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Convert-ToServiceSlug {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    $normalized = $Value.Trim().ToLowerInvariant()
    $normalized = $normalized -replace "[^a-z0-9]+", "-"
    $normalized = $normalized.Trim('-')

    if ([string]::IsNullOrWhiteSpace($normalized)) {
        throw "ServiceName '$Value' does not contain any valid characters."
    }

    return $normalized
}

function Convert-ToPascalCase {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    $parts = $Value -split "[^a-zA-Z0-9]+"
    $builder = New-Object System.Text.StringBuilder

    foreach ($part in $parts) {
        if ([string]::IsNullOrWhiteSpace($part)) {
            continue
        }

        $lower = $part.ToLowerInvariant()
        $first = $lower.Substring(0, 1).ToUpperInvariant()
        $rest = if ($lower.Length -gt 1) { $lower.Substring(1) } else { "" }
        [void]$builder.Append($first + $rest)
    }

    $result = $builder.ToString()
    if ([string]::IsNullOrWhiteSpace($result)) {
        throw "Unable to derive a PascalCase service name from '$Value'."
    }

    return $result
}

function Convert-ToCamelCase {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "Cannot convert an empty value to camelCase."
    }

    if ($Value.Length -eq 1) {
        return $Value.ToLowerInvariant()
    }

    return $Value.Substring(0, 1).ToLowerInvariant() + $Value.Substring(1)
}

function Expand-Template {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Template,

        [Parameter(Mandatory = $true)]
        [hashtable]$Tokens
    )

    $content = $Template
    foreach ($key in $Tokens.Keys) {
        $content = $content.Replace("{{${key}}}", [string]$Tokens[$key])
    }

    return $content
}

function Ensure-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (Test-Path $Path) {
        return
    }

    if ($DryRun) {
        Write-Host "[dry-run] mkdir $Path" -ForegroundColor DarkGray
        return
    }

    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Write-File {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$Content
    )

    $exists = Test-Path $Path
    if ($exists -and -not $Force) {
        throw "File already exists: $Path. Re-run with -Force to overwrite."
    }

    if ($DryRun) {
        $action = if ($exists) { "overwrite" } else { "create" }
        Write-Host "[dry-run] $action file $Path" -ForegroundColor DarkGray
        return
    }

    $parent = Split-Path -Parent $Path
    if (-not (Test-Path $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }

    Set-Content -Path $Path -Value $Content
}

function Add-ProjectToSolutionIfNeeded {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SolutionFile,

        [Parameter(Mandatory = $true)]
        [string]$ProjectPath
    )

    $normalizedProjectPath = $ProjectPath.Replace('/', '\\')
    $unixProjectPath = $normalizedProjectPath.Replace('\\', '/')

    if (-not (Test-Path $SolutionFile)) {
        Write-Warning "Solution file not found: $SolutionFile. Skipping solution registration for $ProjectPath."
        return
    }

    $solutionContent = Get-Content $SolutionFile -Raw
    if (($solutionContent -match [Regex]::Escape($normalizedProjectPath)) -or ($solutionContent -match [Regex]::Escape($unixProjectPath))) {
        return
    }

    if ($DryRun) {
        Write-Host "[dry-run] dotnet sln $SolutionFile add $normalizedProjectPath" -ForegroundColor DarkGray
        return
    }

    dotnet sln $SolutionFile add $normalizedProjectPath | Out-Null
}

function Write-ExistingFileContent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Content
    )

    if ($DryRun) {
        Write-Host "[dry-run] update file $Path" -ForegroundColor DarkGray
        return
    }

    Set-Content -Path $Path -Value $Content
}

function Ensure-BlockInExistingFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Marker,

        [Parameter(Mandatory = $true)]
        [string]$Anchor,

        [Parameter(Mandatory = $true)]
        [string]$Block,

        [Parameter(Mandatory = $false)]
        [ValidateSet("After", "Before")]
        [string]$Position = "After"
    )

    if (-not (Test-Path $Path)) {
        Write-Warning "Skipping update. File does not exist: $Path"
        return $false
    }

    $content = Get-Content $Path -Raw
    if ($content.Contains($Marker)) {
        return $false
    }

    $anchorIndex = $content.IndexOf($Anchor, [StringComparison]::Ordinal)
    if ($anchorIndex -lt 0) {
        Write-Warning "Skipping update in $Path. Anchor was not found."
        return $false
    }

    $insertIndex = if ($Position -eq "After") {
        $anchorIndex + $Anchor.Length
    }
    else {
        $anchorIndex
    }

    $updated = $content.Insert($insertIndex, $Block)
    Write-ExistingFileContent -Path $Path -Content $updated
    return $true
}

function Update-AppHostFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ServiceSlug,

        [Parameter(Mandatory = $true)]
        [string]$ServiceSlugCompact,

        [Parameter(Mandatory = $true)]
        [string]$ServicePascal,

        [Parameter(Mandatory = $true)]
        [string]$ServiceCamel
    )

    $programPath = "src/aspire/Teck.Cloud.AppHost/Program.cs"
    $csprojPath = "src/aspire/Teck.Cloud.AppHost/Teck.Cloud.AppHost.csproj"

    $dbVariable = "$($ServiceCamel)db_postgresWrite"
    $dbResourceName = "$($ServiceSlugCompact)db"
    $migrationVariable = "$($ServiceCamel)Migrations"
    $apiVariable = "$($ServiceCamel)api"
    $apiProjectSymbol = "$($ServicePascal)_Api"

    $dbRegistrationLine = 'var {0} = postgresWrite.AddDatabase("{1}");' -f $dbVariable, $dbResourceName
    $migrationRegistrationLine = 'var {0} = builder.AddProject<Projects.Teck_Cloud_Migrations>("{1}-migrations")' -f $migrationVariable, $ServiceSlug
    $apiRegistrationLine = 'var {0} = builder.AddProject<Projects.{1}>("{2}-api")' -f $apiVariable, $apiProjectSymbol, $ServiceSlug

    [void](Ensure-BlockInExistingFile `
        -Path $programPath `
        -Marker $dbRegistrationLine `
        -Anchor 'var locationdb_postgresWrite = postgresWrite.AddDatabase("locationdb");' `
        -Block ("`r`n" + $dbRegistrationLine))

    $migrationBlock = @"

var $migrationVariable = builder.AddProject<Projects.Teck_Cloud_Migrations>("$ServiceSlug-migrations")
    .WithEnvironment("CONNECTION_STRING", $dbVariable)
    .WithArgs("--service", "$ServiceSlug")
    .WaitFor(postgresWrite);
"@

    [void](Ensure-BlockInExistingFile `
        -Path $programPath `
        -Marker $migrationRegistrationLine `
        -Anchor 'var catalogapi = builder.AddProject<Projects.Catalog_Api>("catalog-api")' `
        -Block $migrationBlock `
        -Position Before)

    $apiBlock = @"

var $apiVariable = builder.AddProject<Projects.$apiProjectSymbol>("$ServiceSlug-api")
    .WithReference($dbVariable, "db-write")
    .WaitForCompletion($migrationVariable)
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("ASPIRE_LOCAL", "true");
"@

    [void](Ensure-BlockInExistingFile `
        -Path $programPath `
        -Marker $apiRegistrationLine `
        -Anchor 'var edgeGateway = builder.AddProject<Projects.Web_Public_Gateway>("web-public-gateway")' `
        -Block $apiBlock `
        -Position Before)

    $appHostReferenceLine = '    <ProjectReference Include="..\..\services\' + $ServiceSlug + '\' + $ServicePascal + '.Api\' + $ServicePascal + '.Api.csproj" />'
    [void](Ensure-BlockInExistingFile `
        -Path $csprojPath `
        -Marker $appHostReferenceLine `
        -Anchor '    <ProjectReference Include="..\..\gateways\Web.Public.Gateway\Web.Public.Gateway.csproj" />' `
        -Block "`r`n$appHostReferenceLine" `
        -Position Before)
}

function Update-MigrationsRunnerFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ServiceSlug,

        [Parameter(Mandatory = $true)]
        [string]$ServicePascal,

        [Parameter(Mandatory = $true)]
        [string]$WriteDbContextName
    )

    $programPath = "src/migrations/Teck.Cloud.Migrations/Program.cs"
    $csprojPath = "src/migrations/Teck.Cloud.Migrations/Teck.Cloud.Migrations.csproj"

    [void](Ensure-BlockInExistingFile `
        -Path $programPath `
        -Marker "using $ServicePascal.Infrastructure.Persistence;" `
        -Anchor 'using Product.Infrastructure.Persistence;' `
        -Block "`r`nusing $ServicePascal.Infrastructure.Persistence;" `
        -Position After)

    $sharedSwitchLine = '        "{0}" => Create{1}Host(connectionString, provider),' -f $ServiceSlug, $ServicePascal
    $dbContextSwitchLine = '        "{0}" => scope.ServiceProvider.GetRequiredService<{1}>(),' -f $ServiceSlug, $WriteDbContextName

    [void](Ensure-BlockInExistingFile `
        -Path $programPath `
        -Marker $sharedSwitchLine `
        -Anchor '        "product" => CreateProductHost(connectionString, provider),' `
        -Block ("`r`n" + $sharedSwitchLine) `
        -Position After)

    [void](Ensure-BlockInExistingFile `
        -Path $programPath `
        -Marker $dbContextSwitchLine `
        -Anchor '        "product" => scope.ServiceProvider.GetRequiredService<ProductWriteDbContext>(),' `
        -Block ("`r`n" + $dbContextSwitchLine) `
        -Position After)

    $dedicatedProductAnchor = @'
            else if (string.Equals(service, "product", StringComparison.OrdinalIgnoreCase))
            {
                hostBuilder = CreateProductHost(writeConnectionString, tenantProvider);
            }
'@

    $dedicatedServiceBlock = @"
            else if (string.Equals(service, "$ServiceSlug", StringComparison.OrdinalIgnoreCase))
            {
                hostBuilder = Create$ServicePascal`Host(writeConnectionString, tenantProvider);
            }
"@

    [void](Ensure-BlockInExistingFile `
        -Path $programPath `
        -Marker "hostBuilder = Create$ServicePascal`Host(writeConnectionString, tenantProvider);" `
        -Anchor $dedicatedProductAnchor `
        -Block "`r`n$dedicatedServiceBlock" `
        -Position After)

    $hostFactoryBlock = @"

static IHostBuilder Create$ServicePascal`Host(string connectionString, DatabaseProvider provider)
{
    string migrationsAssembly = ResolveMigrationsAssembly("$ServicePascal", provider);

    return Host.CreateDefaultBuilder()
        .ConfigureServices(services =>
        {
            services.AddDbContext<$WriteDbContextName>(
                options => ConfigureDbContextOptions(options, connectionString, migrationsAssembly, provider),
                optionsLifetime: ServiceLifetime.Singleton);
        });
}
"@

    [void](Ensure-BlockInExistingFile `
        -Path $programPath `
        -Marker "static IHostBuilder Create$ServicePascal`Host(string connectionString, DatabaseProvider provider)" `
        -Anchor '/// <summary>' `
        -Block $hostFactoryBlock `
        -Position Before)

    $infraReference = '    <ProjectReference Include="..\..\services\' + $ServiceSlug + '\' + $ServicePascal + '.Infrastructure\' + $ServicePascal + '.Infrastructure.csproj" />'

    [void](Ensure-BlockInExistingFile `
        -Path $csprojPath `
        -Marker $infraReference `
        -Anchor '    <ProjectReference Include="..\..\services\product\Product.Infrastructure\Product.Infrastructure.csproj" />' `
        -Block "`r`n$infraReference" `
        -Position After)

function Update-MigrationToolMaps {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ServiceSlug,

        [Parameter(Mandatory = $true)]
        [string]$ServicePascal,

        [Parameter(Mandatory = $true)]
        [string]$WriteDbContextName
    )

    $toolScripts = @(
        "tools/migrations/Add-Migration.ps1",
        "tools/migrations/Remove-Migration.ps1"
    )

    $productEntryAnchor = @'
    "product" = @{
        StartupProject = "src/services/product/Product.Infrastructure/Product.Infrastructure.csproj"
        InfrastructureProject = "src/services/product/Product.Infrastructure/Product.Infrastructure.csproj"
        WriteContextType = "Product.Infrastructure.Persistence.ProductWriteDbContext"
        MigrationProjectPrefix = "src/migrations/Teck.Cloud.Migrations"
        ServicePascal = "Product"
        ExtraCtorArgs = ""
    }
'@

    foreach ($scriptPath in $toolScripts) {
        $serviceEntryBlock = @"

    "$ServiceSlug" = @{
        StartupProject = "src/services/$ServiceSlug/$ServicePascal.Infrastructure/$ServicePascal.Infrastructure.csproj"
        InfrastructureProject = "src/services/$ServiceSlug/$ServicePascal.Infrastructure/$ServicePascal.Infrastructure.csproj"
        WriteContextType = "$ServicePascal.Infrastructure.Persistence.$WriteDbContextName"
        MigrationProjectPrefix = "src/migrations/Teck.Cloud.Migrations"
        ServicePascal = "$ServicePascal"
        ExtraCtorArgs = ""
    }
"@

        [void](Ensure-BlockInExistingFile `
            -Path $scriptPath `
            -Marker ('    "' + $ServiceSlug + '" = @{') `
            -Anchor $productEntryAnchor `
            -Block $serviceEntryBlock `
            -Position After)
    }
}

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDirectory "..\..")
Set-Location $repoRoot

$serviceSlug = Convert-ToServiceSlug -Value $ServiceName
$servicePascal = Convert-ToPascalCase -Value $serviceSlug
$serviceCamel = Convert-ToCamelCase -Value $servicePascal
$serviceSlugCompact = $serviceSlug -replace '-', ''
$solutionFile = "Teck.Cloud.slnx"

$baseServiceDirectory = Join-Path "src/services" $serviceSlug
$apiProjectName = "$servicePascal.Api"
$applicationProjectName = "$servicePascal.Application"
$domainProjectName = "$servicePascal.Domain"
$infrastructureProjectName = "$servicePascal.Infrastructure"
$writeDbContextName = "$servicePascal`WriteDbContext"
$applicationMarkerName = "I$servicePascal`Application"

$tokens = @{
    SERVICE_SLUG = $serviceSlug
    SERVICE_PASCAL = $servicePascal
    API_PROJECT = $apiProjectName
    APPLICATION_PROJECT = $applicationProjectName
    DOMAIN_PROJECT = $domainProjectName
    INFRA_PROJECT = $infrastructureProjectName
    WRITE_DB_CONTEXT = $writeDbContextName
    APPLICATION_MARKER = $applicationMarkerName
}

Write-Host "Scaffolding service '$serviceSlug' ($servicePascal)" -ForegroundColor Cyan
Write-Host "Repository root: $repoRoot" -ForegroundColor DarkCyan

$apiDirectory = Join-Path $baseServiceDirectory $apiProjectName
$applicationDirectory = Join-Path $baseServiceDirectory $applicationProjectName
$domainDirectory = Join-Path $baseServiceDirectory $domainProjectName
$infrastructureDirectory = Join-Path $baseServiceDirectory $infrastructureProjectName

Ensure-Directory -Path $apiDirectory
Ensure-Directory -Path (Join-Path $apiDirectory "Extensions")
Ensure-Directory -Path $applicationDirectory
Ensure-Directory -Path $domainDirectory
Ensure-Directory -Path $infrastructureDirectory
Ensure-Directory -Path (Join-Path $infrastructureDirectory "DependencyInjection")
Ensure-Directory -Path (Join-Path $infrastructureDirectory "Persistence")

$apiCsprojTemplate = @'
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <Product>{{API_PROJECT}}</Product>
    <Description>API for {{SERVICE_SLUG}} service.</Description>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <DockerDefaultTargetOS>Linux</DockerDefaultTargetOS>
    <DockerfileContext>..\..\..\..</DockerfileContext>
    <IsPublishable>true</IsPublishable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="FastEndpoints" />
    <PackageReference Include="FastEndpoints.Swagger" />
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
    <PackageReference Include="Mediator.SourceGenerator">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\aspire\Teck.Cloud.ServiceDefaults\Teck.Cloud.ServiceDefaults.csproj" />
    <ProjectReference Include="..\..\..\buildingblocks\SharedKernel.Core\SharedKernel.Core.csproj" />
    <ProjectReference Include="..\..\..\buildingblocks\SharedKernel.Infrastructure\SharedKernel.Infrastructure.csproj" />
    <ProjectReference Include="..\{{APPLICATION_PROJECT}}\{{APPLICATION_PROJECT}}.csproj" />
    <ProjectReference Include="..\{{INFRA_PROJECT}}\{{INFRA_PROJECT}}.csproj" />
  </ItemGroup>
</Project>
'@

$apiProgramTemplate = @'
// <copyright file="Program.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using FastEndpoints;
using FluentValidation;
using SharedKernel.Infrastructure;
using SharedKernel.Infrastructure.Endpoints;
using SharedKernel.Infrastructure.OpenApi;
using SharedKernel.Infrastructure.Options;
using {{SERVICE_PASCAL}}.Api.Extensions;
using {{SERVICE_PASCAL}}.Application;
using {{SERVICE_PASCAL}}.Infrastructure.DependencyInjection;

namespace {{SERVICE_PASCAL}}.Api;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        Assembly applicationAssembly = typeof({{APPLICATION_MARKER}}).Assembly;
        Assembly apiAssembly = typeof(Program).Assembly;

        AppOptions appOptions = new();
        builder.Configuration.GetSection(AppOptions.Section).Bind(appOptions);

        builder.AddServiceDefaults();
        builder.AddBaseInfrastructure(appOptions);
        builder.AddInfrastructureServices(applicationAssembly);
        builder.Services.AddFastEndpointsInfrastructure(applicationAssembly, apiAssembly);
        builder.Services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);
        builder.Services.AddValidatorsFromAssembly(apiAssembly, includeInternalTypes: true);
        builder.AddMediatorInfrastructure(applicationAssembly);
        builder.AddOpenApiInfrastructure(appOptions);

        WebApplication app = builder.Build();

        app.UseBaseInfrastructure();
        app.UseInfrastructureServices();
        app.UseFastEndpointsInfrastructure("{{SERVICE_SLUG}}");
        app.UseOpenApiInfrastructure(appOptions);
        app.MapDefaultEndpoints();

        await app.RunAsync().ConfigureAwait(false);
    }
}
'@

$apiMediatorExtensionTemplate = @'
// <copyright file="MediatorExtension.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using SharedKernel.Infrastructure.Behaviors;
using {{SERVICE_PASCAL}}.Application;

namespace {{SERVICE_PASCAL}}.Api.Extensions;

internal static class MediatorExtension
{
    public static WebApplicationBuilder AddMediatorInfrastructure(
        this WebApplicationBuilder builder,
        Assembly applicationAssembly)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(applicationAssembly);

        builder.Services.AddMediator(static options =>
        {
            options.Assemblies = [typeof({{APPLICATION_MARKER}})];
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.PipelineBehaviors =
            [
                typeof(LoggingBehavior<,>),
            ];
        });

        return builder;
    }
}
'@

$applicationCsprojTemplate = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <WarningsNotAsErrors>`$(WarningsNotAsErrors);SA1402;SA1518;SA1649</WarningsNotAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\buildingblocks\SharedKernel.Core\SharedKernel.Core.csproj" />
    <ProjectReference Include="..\..\..\buildingblocks\SharedKernel.Events\SharedKernel.Events.csproj" />
    <ProjectReference Include="..\..\..\buildingblocks\SharedKernel.Infrastructure\SharedKernel.Infrastructure.csproj" />
    <ProjectReference Include="..\{{DOMAIN_PROJECT}}\{{DOMAIN_PROJECT}}.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="MemoryPack" />
  </ItemGroup>
</Project>
'@

$applicationMarkerTemplate = @'
// <copyright file="{{APPLICATION_MARKER}}.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

#pragma warning disable SA1633,SA1101,AV2305,IDE0005,CA1515,CA1062,CS1591

namespace {{SERVICE_PASCAL}}.Application;

public interface {{APPLICATION_MARKER}}
{
}
'@

$domainCsprojTemplate = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\buildingblocks\SharedKernel.Core\SharedKernel.Core.csproj" />
    <ProjectReference Include="..\..\..\buildingblocks\SharedKernel.Events\SharedKernel.Events.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="ErrorOr" />
  </ItemGroup>
</Project>
'@

$domainMarkerTemplate = @'
// <copyright file="DomainMarker.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

namespace {{SERVICE_PASCAL}}.Domain;

public static class DomainMarker
{
}
'@

$infrastructureCsprojTemplate = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\buildingblocks\SharedKernel.Core\SharedKernel.Core.csproj" />
    <ProjectReference Include="..\..\..\buildingblocks\SharedKernel.Infrastructure\SharedKernel.Infrastructure.csproj" />
    <ProjectReference Include="..\..\..\buildingblocks\SharedKernel.Persistence\SharedKernel.Persistence.csproj" />
    <ProjectReference Include="..\{{APPLICATION_PROJECT}}\{{APPLICATION_PROJECT}}.csproj" />
    <ProjectReference Include="..\{{DOMAIN_PROJECT}}\{{DOMAIN_PROJECT}}.csproj" />
  </ItemGroup>
</Project>
'@

$infrastructureExtensionsTemplate = @'
// <copyright file="InfrastructureServiceExtensions.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using SharedKernel.Core.Exceptions;
using SharedKernel.Core.Pricing;
using SharedKernel.Infrastructure;
using SharedKernel.Persistence.Database;
using {{SERVICE_PASCAL}}.Infrastructure.Persistence;

namespace {{SERVICE_PASCAL}}.Infrastructure.DependencyInjection;

/// <summary>
/// Registers {{SERVICE_PASCAL}} infrastructure services.
/// </summary>
public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Adds {{SERVICE_PASCAL}} infrastructure dependencies.
    /// </summary>
    /// <param name="builder">Application builder.</param>
    /// <param name="applicationAssembly">Application assembly.</param>
    public static void AddInfrastructureServices(this WebApplicationBuilder builder, Assembly applicationAssembly)
    {
        ValidateInputs(builder, applicationAssembly);
        bool isRunningGeneration = CodeGenerationDetector.IsRunningGeneration();
        ConnectionSettings connectionSettings = ResolveConnectionSettings(builder.Configuration, isRunningGeneration);

        ConfigureDatabase(builder, connectionSettings);
    }

    /// <summary>
    /// Adds {{SERVICE_PASCAL}} infrastructure middleware.
    /// </summary>
    /// <param name="app">Application builder.</param>
    /// <returns>Configured application builder.</returns>
    public static IApplicationBuilder UseInfrastructureServices(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app;
    }

    private static void ValidateInputs(WebApplicationBuilder builder, Assembly applicationAssembly)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(applicationAssembly);
    }

    private static void ConfigureDatabase(WebApplicationBuilder builder, ConnectionSettings settings)
    {
        Assembly dbContextAssembly = typeof({{WRITE_DB_CONTEXT}}).Assembly;
        Assembly migrationsAssembly = ResolveMigrationsAssembly(settings.DatabaseProvider, dbContextAssembly);

        builder.Services.AddDbContextFactory<{{WRITE_DB_CONTEXT}}>(options =>
        {
            SharedKernel.Persistence.Database.Extensions.ConfigureProviderDbContextOptions(
                options,
                settings.WriteConnectionString,
                migrationsAssembly,
                settings.DatabaseProvider);
        });
    }

    private static Assembly ResolveMigrationsAssembly(DatabaseProvider provider, Assembly fallbackAssembly)
    {
        _ = provider;
        return fallbackAssembly;
    }

    private static ConnectionSettings ResolveConnectionSettings(IConfiguration configuration, bool isRunningGeneration)
    {
        DatabaseProvider databaseProvider = configuration.GetDatabaseProvider();
        if (isRunningGeneration)
        {
            return CreateCodeGenerationConnectionSettings(databaseProvider);
        }

        return new ConnectionSettings
        {
            WriteConnectionString = ResolveWriteConnectionString(configuration, databaseProvider),
            DatabaseProvider = databaseProvider,
        };
    }

    private static string ResolveWriteConnectionString(IConfiguration configuration, DatabaseProvider provider)
    {
        _ = provider;
        return configuration.GetConnectionString("db-write")
            ?? throw new ConfigurationMissingException("Database (write)");
    }

    private static ConnectionSettings CreateCodeGenerationConnectionSettings(DatabaseProvider provider)
    {
        string placeholderConnectionString;
        if (provider == DatabaseProvider.SqlServer)
        {
            placeholderConnectionString = "Server=localhost,1433;Database=tempdb;User Id=sa;TrustServerCertificate=True";
        }
        else if (provider == DatabaseProvider.MySQL)
        {
            placeholderConnectionString = "Server=localhost;Port=3306;Database=tempdb;Uid=root;";
        }
        else
        {
            placeholderConnectionString = "Host=localhost;Port=5432;Database=tempdb;Username=postgres";
        }

        return new ConnectionSettings
        {
            WriteConnectionString = placeholderConnectionString,
            DatabaseProvider = provider,
        };
    }

    private sealed record ConnectionSettings
    {
        public string WriteConnectionString { get; init; } = string.Empty;

        public DatabaseProvider DatabaseProvider { get; init; } = default!;
    }
}
'@

$writeDbContextTemplate = @'
// <copyright file="{{WRITE_DB_CONTEXT}}.cs" company="TeckLab">
// Copyright (c) TeckLab. All rights reserved.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace {{SERVICE_PASCAL}}.Infrastructure.Persistence;

/// <summary>
/// EF Core write context for {{SERVICE_PASCAL}}.
/// </summary>
/// <param name="options">DbContext options.</param>
public sealed class {{WRITE_DB_CONTEXT}}(DbContextOptions<{{WRITE_DB_CONTEXT}}> options)
    : DbContext(options)
{
}
'@

Write-File -Path (Join-Path $apiDirectory "$apiProjectName.csproj") -Content (Expand-Template -Template $apiCsprojTemplate -Tokens $tokens)
Write-File -Path (Join-Path $apiDirectory "Program.cs") -Content (Expand-Template -Template $apiProgramTemplate -Tokens $tokens)
Write-File -Path (Join-Path $apiDirectory "Extensions\MediatorExtension.cs") -Content (Expand-Template -Template $apiMediatorExtensionTemplate -Tokens $tokens)

Write-File -Path (Join-Path $applicationDirectory "$applicationProjectName.csproj") -Content (Expand-Template -Template $applicationCsprojTemplate -Tokens $tokens)
Write-File -Path (Join-Path $applicationDirectory "$applicationMarkerName.cs") -Content (Expand-Template -Template $applicationMarkerTemplate -Tokens $tokens)

Write-File -Path (Join-Path $domainDirectory "$domainProjectName.csproj") -Content (Expand-Template -Template $domainCsprojTemplate -Tokens $tokens)
Write-File -Path (Join-Path $domainDirectory "DomainMarker.cs") -Content (Expand-Template -Template $domainMarkerTemplate -Tokens $tokens)

Write-File -Path (Join-Path $infrastructureDirectory "$infrastructureProjectName.csproj") -Content (Expand-Template -Template $infrastructureCsprojTemplate -Tokens $tokens)
Write-File -Path (Join-Path $infrastructureDirectory "DependencyInjection\InfrastructureServiceExtensions.cs") -Content (Expand-Template -Template $infrastructureExtensionsTemplate -Tokens $tokens)
Write-File -Path (Join-Path $infrastructureDirectory "Persistence\$writeDbContextName.cs") -Content (Expand-Template -Template $writeDbContextTemplate -Tokens $tokens)

$createdProjects = @(
    (Join-Path (Join-Path $baseServiceDirectory $apiProjectName) "$apiProjectName.csproj"),
    (Join-Path (Join-Path $baseServiceDirectory $applicationProjectName) "$applicationProjectName.csproj"),
    (Join-Path (Join-Path $baseServiceDirectory $domainProjectName) "$domainProjectName.csproj"),
    (Join-Path (Join-Path $baseServiceDirectory $infrastructureProjectName) "$infrastructureProjectName.csproj")
)

if ($CreateMigrations) {
    $providers = @(
        @{ Suffix = "PostgreSQL"; Method = "UseNpgsql"; Connection = "Host=localhost;Database=teck_migrations;Username=postgres;Password=postgres" },
        @{ Suffix = "SqlServer"; Method = "UseSqlServer"; Connection = "Server=(localdb)\\mssqllocaldb;Database=TeckCloudMigrations;Trusted_Connection=true;MultipleActiveResultSets=true;" },
        @{ Suffix = "MySql"; Method = "UseMySQL"; Connection = "Server=localhost;Database=teck_migrations;User=root;Password=root;" }
    )

    foreach ($provider in $providers) {
        $suffix = [string]$provider.Suffix
        $method = [string]$provider.Method
        $connectionString = [string]$provider.Connection

        $consolidatedProjectDir = Join-Path "src/migrations" "Teck.Cloud.Migrations.$suffix"
        $serviceMigrationDir = Join-Path $consolidatedProjectDir $servicePascal

        Ensure-Directory -Path $serviceMigrationDir
        Ensure-Directory -Path (Join-Path $serviceMigrationDir "DesignTime")
        Ensure-Directory -Path (Join-Path $serviceMigrationDir "Persistence\Migrations")

        $factoryTemplate = @'
    // <auto-generated />
    #pragma warning disable SA1633,SA1515,CS1591

using {{SERVICE_PASCAL}}.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MigrationProject.DesignTime;

public sealed class {{WRITE_DB_CONTEXT}}DesignTimeFactory : IDesignTimeDbContextFactory<{{WRITE_DB_CONTEXT}}>
{
    public {{WRITE_DB_CONTEXT}} CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<{{WRITE_DB_CONTEXT}}>();
        var connectionString = Environment.GetEnvironmentVariable("MIGRATION_CONNECTION_STRING") ?? "{{DEFAULT_CONNECTION}}";
        optionsBuilder.{{EF_METHOD}}(connectionString, b => b.MigrationsAssembly("Teck.Cloud.Migrations.{{SUFFIX}}"));

        return new {{WRITE_DB_CONTEXT}}(optionsBuilder.Options);
    }
}
'@

        $factoryTokens = @{
            SERVICE_PASCAL = $servicePascal
            WRITE_DB_CONTEXT = $writeDbContextName
            DEFAULT_CONNECTION = $connectionString.Replace("\", "\\")
            EF_METHOD = $method
            SUFFIX = $suffix
        }

        Write-File -Path (Join-Path $serviceMigrationDir "DesignTime\$writeDbContextName`DesignTimeFactory.g.cs") -Content (Expand-Template -Template $factoryTemplate -Tokens $factoryTokens)
        Write-File -Path (Join-Path $serviceMigrationDir "Persistence\Migrations\.gitkeep") -Content ""
    }
}

if ($AddToSolution) {
    foreach ($project in $createdProjects) {
        Add-ProjectToSolutionIfNeeded -SolutionFile $solutionFile -ProjectPath $project
    }
}

if ($AutoWire) {
    Update-AppHostFiles -ServiceSlug $serviceSlug -ServiceSlugCompact $serviceSlugCompact -ServicePascal $servicePascal -ServiceCamel $serviceCamel
    Update-MigrationsRunnerFiles -ServiceSlug $serviceSlug -ServicePascal $servicePascal -WriteDbContextName $writeDbContextName
    Update-MigrationToolMaps -ServiceSlug $serviceSlug -ServicePascal $servicePascal -WriteDbContextName $writeDbContextName
}

Write-Host "" -ForegroundColor Gray
Write-Host "Scaffold complete for service '$serviceSlug'." -ForegroundColor Green
Write-Host "Created projects:" -ForegroundColor Green
$createdProjects | ForEach-Object { Write-Host "  - $_" -ForegroundColor Green }

Write-Host "" -ForegroundColor Gray
Write-Host "Suggested next steps:" -ForegroundColor Cyan
if ($AutoWire) {
    Write-Host "  1) Review auto-wired changes in AppHost and migration tooling files." -ForegroundColor Gray
    Write-Host "  2) Run: dotnet restore Teck.Cloud.slnx" -ForegroundColor Gray
    Write-Host "  3) Run: dotnet build Teck.Cloud.slnx -v minimal" -ForegroundColor Gray
}
else {
    Write-Host "  1) Wire AppHost references and migration job entries." -ForegroundColor Gray
    Write-Host "  2) Add service entries to tools/migrations/Add-Migration.ps1 and Remove-Migration.ps1." -ForegroundColor Gray
    Write-Host "  3) Run: dotnet restore Teck.Cloud.slnx" -ForegroundColor Gray
    Write-Host "  4) Run: dotnet build Teck.Cloud.slnx -v minimal" -ForegroundColor Gray
}
