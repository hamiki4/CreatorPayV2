param(
    [Parameter(Mandatory)][ValidatePattern('^https://')][string]$BaseUrl,
    [int]$Attempts = 12,
    [int]$DelaySeconds = 5
)
$ErrorActionPreference = 'Stop'
$base = $BaseUrl.TrimEnd('/')
for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
    try {
        $live = Invoke-RestMethod -Uri "$base/health/live" -TimeoutSec 10
        $ready = Invoke-RestMethod -Uri "$base/health/ready" -TimeoutSec 10
        if ($live.status -eq 'Healthy' -and $ready.status -eq 'Healthy') {
            Write-Output "CreatorPay live and ready at $base (attempt $attempt)."
            exit 0
        }
    } catch {
        if ($attempt -eq $Attempts) { throw }
    }
    Start-Sleep -Seconds $DelaySeconds
}
throw "CreatorPay did not become healthy after $Attempts attempts."
