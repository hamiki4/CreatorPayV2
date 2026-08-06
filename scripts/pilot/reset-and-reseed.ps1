param([Parameter(Mandatory=$true)][ValidateSet("Development","Pilot")][string]$Environment, [switch]$ConfirmReset)
$ErrorActionPreference = "Stop"
if (-not $ConfirmReset) { throw "Reset is destructive. Re-run with -ConfirmReset after confirming this is an isolated pilot database." }
if (-not $env:ConnectionStrings__CreatorPayDatabase) { throw "ConnectionStrings__CreatorPayDatabase is required." }
if ($env:ASPNETCORE_ENVIRONMENT -eq "Production" -or $Environment -eq "Production") { throw "Pilot seeding is permanently disabled in Production." }
$seedFile = Join-Path $PSScriptRoot "seed.sql"
if (-not (Test-Path $seedFile)) { throw "seed.sql is intentionally environment-local because it contains generated temporary password hashes. Generate it using the documented workflow in docs/45-pilot-seeding-and-demo-accounts.md." }
dotnet tool run dotnet-ef database drop --force --project src/CreatorPay.Infrastructure --startup-project src/CreatorPay.Api
dotnet tool run dotnet-ef database update --project src/CreatorPay.Infrastructure --startup-project src/CreatorPay.Api
psql $env:ConnectionStrings__CreatorPayDatabase -v ON_ERROR_STOP=1 -f $seedFile
Write-Host "Pilot database reset and seeded. Run scripts/pilot/verify-pilot.ps1."
