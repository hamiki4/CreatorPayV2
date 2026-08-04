param([Parameter(Mandatory)][string]$DatabaseUrl,[Parameter(Mandatory)][string]$BackupFile,[Parameter(Mandatory)][string]$ExpectedDatabaseName,[switch]$ConfirmRestore)
$ErrorActionPreference = 'Stop'
if (-not $ConfirmRestore) { throw 'Restore is destructive. Re-run with -ConfirmRestore after verifying the target and backup.' }
$resolved = (Resolve-Path -LiteralPath $BackupFile).Path
$manifest = "$resolved.sha256"
if (-not (Test-Path -LiteralPath $manifest)) { throw "Checksum manifest is missing: $manifest" }
$expectedHash = ((Get-Content -LiteralPath $manifest -Raw).Trim() -split '\s+')[0]
$actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $resolved).Hash
if ($expectedHash -ne $actualHash) { throw 'Backup checksum validation failed.' }
$actualDatabaseName = (& psql --dbname=$DatabaseUrl --tuples-only --no-align --command='select current_database();').Trim()
if ($LASTEXITCODE -ne 0 -or $actualDatabaseName -ne $ExpectedDatabaseName) { throw "Restore target mismatch. Connected to '$actualDatabaseName', expected '$ExpectedDatabaseName'." }
& pg_restore --exit-on-error --clean --if-exists --no-owner --no-privileges --dbname=$DatabaseUrl $resolved
if ($LASTEXITCODE -ne 0) { throw 'pg_restore failed.' }
& psql --dbname=$DatabaseUrl --set=ON_ERROR_STOP=1 --command='ANALYZE;'
if ($LASTEXITCODE -ne 0) { throw 'Post-restore ANALYZE failed.' }
Write-Output "Restore completed from checksum-verified backup into '$ExpectedDatabaseName'. Run application integrity and readiness checks before cutover."
