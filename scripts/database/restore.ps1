param([Parameter(Mandatory)][string]$DatabaseUrl,[Parameter(Mandatory)][string]$BackupFile,[switch]$ConfirmRestore)
$ErrorActionPreference = 'Stop'
if (-not $ConfirmRestore) { throw 'Restore is destructive. Re-run with -ConfirmRestore after verifying the target and backup.' }
$resolved = (Resolve-Path -LiteralPath $BackupFile).Path
& pg_restore --clean --if-exists --no-owner --no-privileges --dbname=$DatabaseUrl $resolved
if ($LASTEXITCODE -ne 0) { throw 'pg_restore failed.' }
