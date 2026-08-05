param(
    [string]$ApiBaseUrl = "http://localhost:8080",
    [string]$ConnectionString = $env:ConnectionStrings__CreatorPay,
    [switch]$SkipTests
)
$ErrorActionPreference = "Stop"
$started = Get-Date
$resultDirectory = Join-Path $PSScriptRoot "results"
New-Item -ItemType Directory -Force -Path $resultDirectory | Out-Null
$resultFile = Join-Path $resultDirectory ("pilot-{0}.json" -f $started.ToUniversalTime().ToString("yyyyMMdd-HHmmss"))
$checks = [System.Collections.Generic.List[object]]::new()
function Invoke-Check([string]$Name, [scriptblock]$Action) {
    try { & $Action | Out-Null; $checks.Add([pscustomobject]@{ name=$Name; passed=$true; detail="OK" }) }
    catch { $checks.Add([pscustomobject]@{ name=$Name; passed=$false; detail=$_.Exception.Message }) }
}
Invoke-Check "API live" { Invoke-RestMethod "$ApiBaseUrl/health/live" -TimeoutSec 10 }
Invoke-Check "API and database ready" { Invoke-RestMethod "$ApiBaseUrl/health/ready" -TimeoutSec 10 }
if ($ConnectionString) {
    Invoke-Check "Migrations applied" { dotnet tool run dotnet-ef migrations list --project src/CreatorPay.Infrastructure --startup-project src/CreatorPay.Api --connection $ConnectionString --no-build --configuration Release | Out-String }
    Invoke-Check "Seed manifest present" { psql $ConnectionString -v ON_ERROR_STOP=1 -Atc 'SELECT count(*) FROM "UserAccounts" WHERE "Email" LIKE ''%@pilot.creatorpay.test'';' | Where-Object { [int]$_ -ge 10 } | ForEach-Object { return }; throw "Expected at least ten pilot accounts." }
} else { $checks.Add([pscustomobject]@{ name="Database checks"; passed=$false; detail="ConnectionStrings__CreatorPay is not set (value is never written to results)." }) }
if (-not $SkipTests) {
    Invoke-Check "Core financial and pilot tests" { dotnet test CreatorPay.slnx --configuration Release --no-build --filter "FullyQualifiedName~FourParty|FullyQualifiedName~Checkout|FullyQualifiedName~Pilot" }
}
$passed = -not ($checks | Where-Object { -not $_.passed })
[pscustomobject]@{ schemaVersion=1; startedAtUtc=$started.ToUniversalTime(); finishedAtUtc=(Get-Date).ToUniversalTime(); passed=$passed; checks=$checks } |
    ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 $resultFile
Write-Host ("Pilot verification: {0}`nResult: {1}" -f $(if ($passed) {"PASS"} else {"FAIL"}), $resultFile)
if (-not $passed) { exit 1 }
