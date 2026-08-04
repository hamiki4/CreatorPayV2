param([Parameter(Mandatory)][string]$DatabaseUrl,[Parameter(Mandatory)][string]$OutputFile)
$ErrorActionPreference = 'Stop'
$resolved = [IO.Path]::GetFullPath($OutputFile); New-Item -ItemType Directory -Force -Path (Split-Path -Parent $resolved) | Out-Null
& pg_dump --format=custom --no-owner --no-privileges --file=$resolved --dbname=$DatabaseUrl
if ($LASTEXITCODE -ne 0) { throw 'pg_dump failed.' }
Write-Output "Encrypted storage is required for backup: $resolved"
