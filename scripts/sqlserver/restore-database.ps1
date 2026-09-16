param(
    [string]$Server = ".\SQLEXPRESS",
    [string]$Database = "EduPlatformDB",
    [Parameter(Mandatory = $true)]
    [string]$BackupFile
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path $BackupFile)) {
    throw "Backup file not found: $BackupFile"
}

$resolvedBackup = (Resolve-Path $BackupFile).Path.Replace("'", "''")
$sql = @"
ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [$Database] FROM DISK = N'$resolvedBackup' WITH REPLACE, RECOVERY;
ALTER DATABASE [$Database] SET MULTI_USER;
"@

sqlcmd -S $Server -E -b -Q $sql
if ($LASTEXITCODE -ne 0) {
    throw "SQL Server restore failed."
}

Write-Host "Database restored: $Database" -ForegroundColor Green
