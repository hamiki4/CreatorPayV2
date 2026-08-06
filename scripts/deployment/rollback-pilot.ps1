param(
    [Parameter(Mandatory)][string]$PreviousReleaseDirectory,
    [string]$EnvironmentFile = '/opt/weymela/.env.pilot',
    [Parameter(Mandatory)][ValidatePattern('^https://')][string]$ApiUrl,
    [Parameter(Mandatory)][ValidatePattern('^https://')][string]$WebUrl,
    [switch]$ConfirmRollback
)
$ErrorActionPreference = 'Stop'
if (-not $ConfirmRollback) { throw 'Re-run with -ConfirmRollback after confirming application/schema compatibility and incident approval.' }
$release = (Resolve-Path -LiteralPath $PreviousReleaseDirectory).Path
if (-not (Test-Path -LiteralPath (Join-Path $release 'docker-compose.yml'))) { throw 'Previous release does not contain docker-compose.yml.' }
if (-not (Test-Path -LiteralPath $EnvironmentFile)) { throw "Environment file not found: $EnvironmentFile" }
$original = Get-Location
$previousMaintenance = $env:FeatureFlags__MaintenanceMode
try {
    Set-Location $release
    $compose = @('-f','docker-compose.yml','-f','docker-compose.pilot.yml','--env-file',$EnvironmentFile)
    docker compose @compose config --quiet
    if ($LASTEXITCODE) { throw 'Previous release Compose validation failed.' }
    $env:FeatureFlags__MaintenanceMode = 'true'
    docker compose @compose up -d --build api
    $env:FeatureFlags__MaintenanceMode = 'false'
    docker compose @compose up -d --build api worker web
    & scripts/operations/readiness-smoke.ps1 -BaseUrl $ApiUrl
    & scripts/pilot/smoke-test.ps1 -ApiUrl $ApiUrl -WebUrl $WebUrl
} finally {
    $env:FeatureFlags__MaintenanceMode = $previousMaintenance
    Set-Location $original
}
Write-Output 'Previous application release is healthy. No database down-migration or restore was performed; complete financial reconciliation before reopening Pilot checkout.'
