param(
    [string]$Server = ".\SQLEXPRESS",
    [string]$Database = "EduPlatformDB",
    [string]$BackupDirectory = ".\backups"
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Path $BackupDirectory -Force | Out-Null
$backupFile = Join-Path (Resolve-Path $BackupDirectory) "$Database-$(Get-Date -Format 'yyyyMMdd-HHmmss').bak"
$escapedPath = $backupFile.Replace("'", "''")

sqlcmd -S $Server -E -b -Q "BACKUP DATABASE [$Database] TO DISK = N'$escapedPath' WITH INIT, CHECKSUM, STATS = 10"
if ($LASTEXITCODE -ne 0) {
    throw "SQL Server backup failed."
}

Write-Host "Backup created: $backupFile" -ForegroundColor Green
