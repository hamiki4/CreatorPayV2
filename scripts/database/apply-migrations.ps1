param([Parameter(Mandatory)][string]$ConnectionString)
$ErrorActionPreference = 'Stop'
dotnet ef database update --project src/CreatorPay.Infrastructure --startup-project src/CreatorPay.Api --connection $ConnectionString
