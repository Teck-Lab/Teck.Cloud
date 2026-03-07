# Add-Migration-PerService.ps1
# Backward-compatible wrapper for Add-Migration.ps1.
# Generates EF Core migrations directly into each service Infrastructure project.

param (
    [Parameter(Mandatory=$true)]
    [string]$ServiceName,

    [Parameter(Mandatory=$true)]
    [string]$MigrationName,

    [Parameter(Mandatory=$false)]
    [switch]$ChangeToSolutionDir,

    [Parameter(Mandatory=$false)]
    [string[]]$Providers = @("postgres", "sqlserver", "mysql")
)

if ($ChangeToSolutionDir -or -not $PSBoundParameters.ContainsKey('ChangeToSolutionDir')) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $solutionDir = Resolve-Path (Join-Path $scriptDir "..\..")
    Set-Location $solutionDir
    Write-Host "Working directory set to: $solutionDir" -ForegroundColor Cyan
}

$addMigrationScript = Join-Path $PSScriptRoot "Add-Migration.ps1"
if (-not (Test-Path $addMigrationScript)) {
    Write-Error "Could not find Add-Migration.ps1 next to this script."
    exit 1
}

Write-Host "Delegating to Add-Migration.ps1 (per-service migration mode)." -ForegroundColor Yellow
& $addMigrationScript -ServiceName $ServiceName -MigrationName $MigrationName -Providers $Providers
exit $LASTEXITCODE
