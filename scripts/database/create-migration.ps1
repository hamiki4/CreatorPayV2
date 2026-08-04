param([Parameter(Mandatory)][ValidatePattern('^[A-Za-z][A-Za-z0-9]+$')][string]$Name)
$ErrorActionPreference = 'Stop'
dotnet tool restore
dotnet tool run dotnet-ef migrations add $Name --project src/CreatorPay.Infrastructure --startup-project src/CreatorPay.Api
