param(
    [string]$HealthUrl = "http://localhost:5053/health/details",
    [int]$IntervalSeconds = 30,
    [int]$Checks = 1,
    [string]$WebhookUrl = ""
)

$ErrorActionPreference = "Stop"

for ($index = 1; $index -le $Checks; $index++) {
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    try {
        $health = Invoke-RestMethod -Uri $HealthUrl -TimeoutSec 15
        if ($health.status -ne "healthy") {
            $message = "EduPlatform degraded at ${timestamp}: $($health | ConvertTo-Json -Compress)"
            Write-Warning $message
            if (-not [string]::IsNullOrWhiteSpace($WebhookUrl)) {
                Invoke-RestMethod -Uri $WebhookUrl -Method Post -ContentType "application/json" `
                    -Body (@{ text = $message } | ConvertTo-Json)
            }
            exit 1
        }

        Write-Host "$timestamp EduPlatform healthy" -ForegroundColor Green
    }
    catch {
        $message = "EduPlatform health unavailable at ${timestamp}: $($_.Exception.Message)"
        Write-Warning $message
        if (-not [string]::IsNullOrWhiteSpace($WebhookUrl)) {
            Invoke-RestMethod -Uri $WebhookUrl -Method Post -ContentType "application/json" `
                -Body (@{ text = $message } | ConvertTo-Json)
        }
        exit 1
    }

    if ($index -lt $Checks) {
        Start-Sleep -Seconds $IntervalSeconds
    }
}
