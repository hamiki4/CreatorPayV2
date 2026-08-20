param([string]$ConnectionString = $env:ConnectionStrings__CreatorPayDatabase)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ConnectionString)) { throw 'Set ConnectionStrings__CreatorPayDatabase before running this script.' }
dotnet tool restore
dotnet restore CreatorPay.slnx
dotnet tool run dotnet-ef database update --project src/CreatorPay.Infrastructure --startup-project src/CreatorPay.Api --connection $ConnectionString
dotnet tool run dotnet-ef migrations list --project src/CreatorPay.Infrastructure --startup-project src/CreatorPay.Api --connection $ConnectionString
