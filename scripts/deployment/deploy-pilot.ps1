param(
    [string]$EnvironmentFile = '/opt/weymela/.env.pilot',
    [string]$BackupDirectory = '/opt/weymela/backups',
    [Parameter(Mandatory)][ValidatePattern('^https://')][string]$ApiUrl,
    [Parameter(Mandatory)][ValidatePattern('^https://')][string]$WebUrl
)
$ErrorActionPreference = 'Stop'
$compose = @('-f','docker-compose.yml','-f','docker-compose.pilot.yml','--env-file',$EnvironmentFile)
if (-not (Test-Path -LiteralPath $EnvironmentFile)) { throw "Environment file not found: $EnvironmentFile" }
$required = 'POSTGRES_PASSWORD','Authentication__Jwt__SigningKey','CustomerVerification__HmacSecret','CustomerVerification__EncryptionKey','WEB_ORIGIN','VITE_API_URL','Support__Email'
$values = @{}
Get-Content -LiteralPath $EnvironmentFile | Where-Object { $_ -match '^[^#][^=]*=' } | ForEach-Object { $key,$value = $_ -split '=',2; $values[$key.Trim()] = $value.Trim() }
foreach ($key in $required) { if (-not $values[$key] -or $values[$key] -match '(?i)replace[-_ ]?(with|in)|placeholder|change[-_ ]?me') { throw "Required non-placeholder setting is missing: $key" } }
$secretKeys = 'Authentication__Jwt__SigningKey','CustomerVerification__HmacSecret','CustomerVerification__EncryptionKey'
foreach ($key in $secretKeys) { if ($values[$key].Length -lt 32) { throw "$key must contain at least 32 characters." } }
if ($values.POSTGRES_PASSWORD.Length -lt 24) { throw 'POSTGRES_PASSWORD must contain at least 24 characters.' }
if (($secretKeys | ForEach-Object { $values[$_] } | Sort-Object -Unique).Count -ne $secretKeys.Count) { throw 'JWT, HMAC and encryption secrets must be distinct.' }
docker compose @compose config --quiet
if ($LASTEXITCODE) { throw 'Compose validation failed.' }
$dbUser = if ($values.POSTGRES_USER) { $values.POSTGRES_USER } else { 'creatorpay' }
$dbName = if ($values.POSTGRES_DB) { $values.POSTGRES_DB } else { 'CreatorPayV2Db' }
$dbPort = if ($values.POSTGRES_PORT) { $values.POSTGRES_PORT } else { '5432' }
$dbUrl = "postgresql://$([Uri]::EscapeDataString($dbUser)):$([Uri]::EscapeDataString($values.POSTGRES_PASSWORD))@127.0.0.1:$dbPort/$([Uri]::EscapeDataString($dbName))"
$backup = $null
if ((docker compose @compose ps --status running --services) -contains 'postgres') {
    New-Item -ItemType Directory -Force -Path $BackupDirectory | Out-Null
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $backup = Join-Path $BackupDirectory "creatorpay-$stamp.dump"
    & scripts/database/backup.ps1 -DatabaseUrl $dbUrl -OutputFile $backup
}
$previousMaintenance = $env:FeatureFlags__MaintenanceMode
try {
    $env:FeatureFlags__MaintenanceMode = 'true'
    docker compose @compose up -d postgres
    dotnet tool restore
    $env:ConnectionStrings__CreatorPayDatabase = $dbUrl
    dotnet tool run dotnet-ef database update --project src/CreatorPay.Infrastructure --startup-project src/CreatorPay.Api
    $env:FeatureFlags__MaintenanceMode = 'false'
    docker compose @compose up -d --build api worker web
    & scripts/operations/readiness-smoke.ps1 -BaseUrl $ApiUrl
    & scripts/pilot/smoke-test.ps1 -ApiUrl $ApiUrl -WebUrl $WebUrl
} finally { $env:FeatureFlags__MaintenanceMode = $previousMaintenance }
$backupMessage = if ($backup) { "Backup retained at $backup." } else { 'No pre-deployment backup was required for the first empty deployment.' }
Write-Output "Pilot deployment health checks passed. $backupMessage Complete the manual financial smoke test before go-live approval."
