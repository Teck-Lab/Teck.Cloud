# Remove-Migration-PerService.ps1
# Remove the last EF Core migration for a specific service's Infrastructure project (per-provider)

param (
    [Parameter(Mandatory=$true)]
    [string]$ServiceName,

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

$removeMigrationScript = Join-Path $PSScriptRoot "Remove-Migration.ps1"
if (-not (Test-Path $removeMigrationScript)) {
    Write-Error "Could not find Remove-Migration.ps1 next to this script."
    exit 1
}

Write-Host "Delegating to Remove-Migration.ps1 (per-service migration mode)." -ForegroundColor Yellow
& $removeMigrationScript -ServiceName $ServiceName -Providers $Providers
exit $LASTEXITCODE
