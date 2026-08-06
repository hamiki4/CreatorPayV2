param([Parameter(Mandatory=$true)][string]$ApiUrl,[Parameter(Mandatory=$true)][string]$WebUrl)
$ErrorActionPreference = 'Stop'
if (-not $ApiUrl.StartsWith('https://') -or -not $WebUrl.StartsWith('https://')) { throw 'Pilot smoke tests require HTTPS URLs.' }
$live = Invoke-RestMethod "$ApiUrl/health/live"
$ready = Invoke-RestMethod "$ApiUrl/health/ready"
$web = Invoke-WebRequest "$WebUrl/healthz"
if ($live.status -ne 'Healthy' -or $ready.status -ne 'Healthy' -or $web.StatusCode -ne 200) { throw 'Pilot infrastructure smoke test failed.' }
Write-Host 'Infrastructure passed. Complete the authenticated financial checks in docs/63-private-pilot-operations.md.'
