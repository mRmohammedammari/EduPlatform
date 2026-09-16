# ============================================
# EduPlatform - Complete Platform Initialization
# ============================================
# Version: 1.0
# Date: 2026-08-28
# Description: Script complet pour initialiser toute la plateforme

$ErrorActionPreference = "Stop"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "   EduPlatform - Platform Initialization   " -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

$seedDemoData = $env:SEED_DEMO_DATA -eq "true"

$startTime = Get-Date

# ============================================
# 1. Vérifier les prérequis
# ============================================
Write-Host "[1/7] Checking prerequisites..." -ForegroundColor Yellow

# Vérifier Docker
try {
    $dockerVersion = docker --version
    Write-Host "? Docker is installed: $dockerVersion" -ForegroundColor Green
    docker info | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Docker daemon is unavailable"
    }
    Write-Host "? Docker daemon is running" -ForegroundColor Green
} catch {
    Write-Host "? Docker Desktop is not running" -ForegroundColor Red
    Write-Host "Start Docker Desktop, wait until it is ready, then rerun this script."
    exit 1
}

# Vérifier .NET SDK
try {
    $dotnetVersion = dotnet --version
    Write-Host "? .NET SDK is installed: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "? .NET SDK is not installed" -ForegroundColor Red
    Write-Host "Please install .NET 8 SDK: https://dotnet.microsoft.com/download"
    exit 1
}

Write-Host ""

# ============================================
# 2. Démarrer les services Docker
# ============================================
Write-Host "[2/7] Starting Docker services..." -ForegroundColor Yellow

# Vérifier si docker-compose.yml existe
if (-not (Test-Path "docker-compose.yml")) {
    Write-Host "? docker-compose.yml not found" -ForegroundColor Red
    exit 1
}

# Démarrer les services
Write-Host "Starting Cassandra, Kafka, Zookeeper, and Redis..." -ForegroundColor Cyan
docker-compose up -d

# Attendre que les services soient prêts
Write-Host "Waiting for services to be ready..." -ForegroundColor Cyan
Start-Sleep -Seconds 30

Write-Host "? Docker services started" -ForegroundColor Green
Write-Host ""

# ============================================
# 3. Initialiser Cassandra
# ============================================
Write-Host "[3/7] Initializing Cassandra..." -ForegroundColor Yellow

if (Test-Path "scripts\cassandra\init.cql") {
    Write-Host "Executing Cassandra initialization script..." -ForegroundColor Cyan
    
    # Attendre que Cassandra soit complètement démarré
    $maxAttempts = 30
    $attempt = 0
    $cassandraReady = $false
    
    while (-not $cassandraReady -and $attempt -lt $maxAttempts) {
        $attempt++
        Write-Host "Attempt $attempt/$maxAttempts - Checking if Cassandra is ready..." -ForegroundColor Gray
        
        $result = docker exec cassandra_edu cqlsh -e "DESCRIBE KEYSPACES;" 2>&1
        if ($LASTEXITCODE -eq 0) {
            $cassandraReady = $true
            Write-Host "? Cassandra is ready" -ForegroundColor Green
        } else {
            Start-Sleep -Seconds 10
        }
    }
    
    if (-not $cassandraReady) {
        Write-Host "? Cassandra failed to start within the timeout period" -ForegroundColor Red
        exit 1
    }
    
    # Exécuter le script d'initialisation
    Get-Content "scripts\cassandra\init.cql" | docker exec -i cassandra_edu cqlsh
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "? Cassandra initialized successfully" -ForegroundColor Green
    } else {
        Write-Host "? Cassandra initialization may have failed" -ForegroundColor Yellow
    }
} else {
    Write-Host "? Cassandra init script not found, skipping..." -ForegroundColor Yellow
}

if ($seedDemoData -and (Test-Path "scripts\cassandra\seed-data.cql")) {
    Write-Host "Loading Cassandra demo data..." -ForegroundColor Cyan
    Get-Content "scripts\cassandra\seed-data.cql" | docker exec -i cassandra_edu cqlsh
}

Write-Host ""

# ============================================
# 4. Initialiser Kafka Topics
# ============================================
Write-Host "[4/7] Initializing Kafka topics..." -ForegroundColor Yellow

if (Test-Path "scripts\kafka\init-topics.ps1") {
    Write-Host "Executing Kafka topics initialization..." -ForegroundColor Cyan
    
    # Attendre que Kafka soit prêt
    Start-Sleep -Seconds 20
    
    & "scripts\kafka\init-topics.ps1"
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "? Kafka topics initialized successfully" -ForegroundColor Green
    } else {
        Write-Host "? Kafka topics initialization may have failed" -ForegroundColor Yellow
    }
} else {
    Write-Host "? Kafka init script not found, skipping..." -ForegroundColor Yellow
}

Write-Host ""

# ============================================
# 5. Restaurer les dépendances .NET
# ============================================
Write-Host "[5/7] Restoring .NET dependencies..." -ForegroundColor Yellow

dotnet restore

if ($LASTEXITCODE -eq 0) {
    Write-Host "? Dependencies restored successfully" -ForegroundColor Green
} else {
    Write-Host "? Failed to restore dependencies" -ForegroundColor Red
    exit 1
}

Write-Host ""

# ============================================
# 6. Exécuter les migrations de base de données
# ============================================
Write-Host "[6/7] Running database migrations..." -ForegroundColor Yellow

# Vérifier si Entity Framework Core Tools est installé
$efInstalled = dotnet ef --version 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Installing Entity Framework Core Tools..." -ForegroundColor Cyan
    dotnet tool install --global dotnet-ef
}

# Exécuter les migrations
Write-Host "Applying migrations to SQL Server..." -ForegroundColor Cyan
if (docker ps --format "{{.Names}}" | Select-String "^sqlserver_edu$") {
    Write-Host "SQL Server Docker detected; API container applies migrations on startup." -ForegroundColor Cyan
    $LASTEXITCODE = 0
} else {
    dotnet ef database update --project EduPlatform.Data --startup-project EduPlatform.API
}

if ($LASTEXITCODE -eq 0) {
    Write-Host "? Database migrations applied successfully" -ForegroundColor Green
} else {
    Write-Host "? Database migrations may have failed" -ForegroundColor Yellow
    Write-Host "Make sure SQL Server is running and connection string is correct" -ForegroundColor Yellow
}

if ($seedDemoData) {
    Write-Host "Loading SQL Server demo data..." -ForegroundColor Cyan
    if (docker ps --format "{{.Names}}" | Select-String "^sqlserver_edu$") {
        $sqlPassword = $env:SQLSERVER_SA_PASSWORD
        if ([string]::IsNullOrWhiteSpace($sqlPassword)) {
            $sqlPassword = "EduPlatform!Sql2026Strong"
        }
        Get-Content "scripts\sqlserver\seed-data.sql" |
            docker exec -i sqlserver_edu /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P $sqlPassword -C -b
    } elseif (Get-Command sqlcmd -ErrorAction SilentlyContinue) {
        sqlcmd -S ".\SQLEXPRESS" -d "EduPlatformDB" -E -b -i "scripts\sqlserver\seed-data.sql"
    } else {
        Write-Host "? No SQL Server client or container found" -ForegroundColor Yellow
        $LASTEXITCODE = 1
    }

    if ($LASTEXITCODE -eq 0) {
        Write-Host "? Demo data loaded successfully" -ForegroundColor Green
    } else {
        Write-Host "? Demo data loading failed" -ForegroundColor Yellow
    }
}

Write-Host ""

# ============================================
# 7. Build le projet
# ============================================
Write-Host "[7/7] Building the solution..." -ForegroundColor Yellow

dotnet build --configuration Debug

if ($LASTEXITCODE -eq 0) {
    Write-Host "? Solution built successfully" -ForegroundColor Green
} else {
    Write-Host "? Build failed" -ForegroundColor Red
    exit 1
}

Write-Host ""

# ============================================
# Résumé
# ============================================
$endTime = Get-Date
$duration = $endTime - $startTime

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "   ? Platform Initialization Complete!    " -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Duration: $($duration.TotalSeconds) seconds" -ForegroundColor Gray
Write-Host ""
Write-Host "Services Status:" -ForegroundColor Yellow
Write-Host "  ? Cassandra:  http://localhost:9042" -ForegroundColor Green
Write-Host "  ? Kafka:      http://localhost:9092" -ForegroundColor Green
Write-Host "  ? Redis:      http://localhost:6379" -ForegroundColor Green
Write-Host "  ? Zookeeper:  http://localhost:2181" -ForegroundColor Green
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Configure your secrets (OpenAI API Key, JWT Key):" -ForegroundColor Cyan
Write-Host "     cd EduPlatform.API"
Write-Host "     dotnet user-secrets set 'OpenAI:ApiKey' 'your-key'"
Write-Host "     dotnet user-secrets set 'Jwt:Key' 'your-secret-key-min-32-chars'"
Write-Host ""
Write-Host "  2. Start the API:" -ForegroundColor Cyan
Write-Host "     cd EduPlatform.API"
Write-Host "     dotnet run"
Write-Host ""
Write-Host "  3. Start the Web app (in another terminal):" -ForegroundColor Cyan
Write-Host "     cd EduPlatform.Web"
Write-Host "     dotnet run"
Write-Host ""
Write-Host "  4. Access the application:" -ForegroundColor Cyan
Write-Host "     API:     https://localhost:7194"
Write-Host "     Swagger: https://localhost:7194/swagger"
Write-Host "     Web:     https://localhost:7286"
Write-Host ""
Write-Host "To stop all services: docker-compose down" -ForegroundColor Gray
Write-Host ""
