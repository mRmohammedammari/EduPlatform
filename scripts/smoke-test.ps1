param(
    [string]$ApiUrl = "http://localhost:5053",
    [string]$WebUrl = "http://localhost:5297",
    [string]$Email = "student@eduplatform.com",
    [string]$Password = "Student123!"
)

$ErrorActionPreference = "Stop"

function Assert-Status([string]$Name, [bool]$Condition) {
    if (-not $Condition) { throw "$Name failed" }
    Write-Host "$Name OK" -ForegroundColor Green
}

$health = Invoke-RestMethod "$ApiUrl/health/details"
Assert-Status "API health" ($health.status -eq "healthy")
Assert-Status "SQL Server" ($health.checks.sqlServer.status -eq "healthy")
Assert-Status "Cassandra" ($health.checks.cassandra.status -eq "healthy")
Assert-Status "Redis" ($health.checks.redis.status -eq "healthy")
Assert-Status "Kafka" ($health.checks.kafka.status -eq "healthy")

$web = Invoke-WebRequest $WebUrl -UseBasicParsing
Assert-Status "Web frontend" ($web.StatusCode -eq 200)

$login = Invoke-RestMethod "$ApiUrl/api/auth/login" -Method Post `
    -ContentType "application/json" `
    -Body (@{ email = $Email; password = $Password } | ConvertTo-Json)
Assert-Status "Student login" (-not [string]::IsNullOrWhiteSpace($login.token))

$headers = @{ Authorization = "Bearer $($login.token)" }
$courses = Invoke-RestMethod "$ApiUrl/api/courses" -Headers $headers
Assert-Status "Course catalog" ($courses.Count -gt 0)

$adminLogin = Invoke-RestMethod "$ApiUrl/api/auth/login" -Method Post `
    -ContentType "application/json" `
    -Body (@{ email = "admin@eduplatform.com"; password = "Admin123!" } | ConvertTo-Json)
$adminHeaders = @{ Authorization = "Bearer $($adminLogin.token)" }
$overview = Invoke-RestMethod "$ApiUrl/api/admin/overview" -Headers $adminHeaders
Assert-Status "Admin overview" ($overview.users -gt 0 -and $overview.courses -gt 0)

$instructorLogin = Invoke-RestMethod "$ApiUrl/api/auth/login" -Method Post `
    -ContentType "application/json" `
    -Body (@{ email = "instructor@eduplatform.com"; password = "Instructor123!" } | ConvertTo-Json)
$instructorHeaders = @{ Authorization = "Bearer $($instructorLogin.token)" }
$ownedCourses = Invoke-RestMethod "$ApiUrl/api/courses/mine" -Headers $instructorHeaders
Assert-Status "Instructor courses" ($ownedCourses.Count -gt 0)

$courseId = "44444444-4444-4444-4444-444444444444"
$enrolled = Invoke-RestMethod "$ApiUrl/api/courses/$courseId/enrollment-status" -Headers $headers
Assert-Status "Student enrollment" ($enrolled -eq $true)
$questions = Invoke-RestMethod "$ApiUrl/api/tests/$courseId" -Headers $headers
Assert-Status "Course questions" ($questions.Count -gt 0)
$notifications = Invoke-RestMethod "$ApiUrl/api/notifications" -Headers $headers
Assert-Status "Student notifications" ($null -ne $notifications)

Write-Host "Smoke test completed successfully: $($courses.Count) course(s), $($questions.Count) question(s)." -ForegroundColor Cyan
