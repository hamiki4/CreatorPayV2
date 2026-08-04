param([Parameter(Mandatory)][string]$DatabaseUrl,[Parameter(Mandatory)][string]$OutputFile)
$ErrorActionPreference = 'Stop'
$resolved = [IO.Path]::GetFullPath($OutputFile); New-Item -ItemType Directory -Force -Path (Split-Path -Parent $resolved) | Out-Null
& pg_dump --format=custom --compress=9 --no-owner --no-privileges --file=$resolved --dbname=$DatabaseUrl
if ($LASTEXITCODE -ne 0) { throw 'pg_dump failed.' }
$checksum = (Get-FileHash -Algorithm SHA256 -LiteralPath $resolved).Hash.ToLowerInvariant()
Set-Content -LiteralPath "$resolved.sha256" -Value "$checksum  $([IO.Path]::GetFileName($resolved))" -Encoding ascii
Write-Output "Backup and SHA-256 manifest created. Transfer both to encrypted immutable storage: $resolved"
