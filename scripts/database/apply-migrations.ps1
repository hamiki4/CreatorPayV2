param([Parameter(Mandatory)][string]$ConnectionString)
$ErrorActionPreference = 'Stop'
dotnet tool restore
dotnet tool run dotnet-ef database update --project src/CreatorPay.Infrastructure --startup-project src/CreatorPay.Api --connection $ConnectionString
