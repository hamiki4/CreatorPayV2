param([string]$ConnectionString = $env:ConnectionStrings__CreatorPayDatabase)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ConnectionString)) { throw 'Set ConnectionStrings__CreatorPayDatabase before running this script.' }
dotnet ef database update --project src/CreatorPay.Infrastructure --startup-project src/CreatorPay.Api --connection $ConnectionString
dotnet ef migrations list --project src/CreatorPay.Infrastructure --startup-project src/CreatorPay.Api --connection $ConnectionString
