param([string]$Output = 'artifacts/database/creatorpay-migration.sql')
$ErrorActionPreference = 'Stop'
$resolved = [IO.Path]::GetFullPath((Join-Path $PWD $Output)); $directory = Split-Path -Parent $resolved
New-Item -ItemType Directory -Force -Path $directory | Out-Null
dotnet tool restore
dotnet tool run dotnet-ef migrations script --idempotent --project src/CreatorPay.Infrastructure --startup-project src/CreatorPay.Api --output $resolved
