# ============================================
# EduPlatform Kafka Topics Initialization (PowerShell)
# ============================================
# Version: 1.0
# Date: 2026-08-28
# Description: Crée tous les topics Kafka nécessaires

$ErrorActionPreference = "Stop"

$KAFKA_CONTAINER = "kafka_edu"
$BOOTSTRAP_SERVER = "localhost:9092"

Write-Host "============================================" -ForegroundColor Blue
Write-Host "  EduPlatform Kafka Topics Initialization" -ForegroundColor Blue
Write-Host "============================================" -ForegroundColor Blue
Write-Host ""

# Vérifier que Kafka est démarré
Write-Host "Checking if Kafka is running..." -ForegroundColor Yellow
$kafkaRunning = docker ps --filter "name=$KAFKA_CONTAINER" --format "{{.Names}}"
if (-not $kafkaRunning) {
    Write-Host "Error: Kafka container is not running" -ForegroundColor Red
    Write-Host "Please start Kafka with: docker-compose up -d kafka"
    exit 1
}
Write-Host "? Kafka is running" -ForegroundColor Green
Write-Host ""

# Configuration des topics (Topic => Partitions:Retention)
$topics = @{
    "user-activity-events" = "3:604800000"      # 3 partitions, 7 jours retention
    "test-results-events" = "3:2592000000"      # 3 partitions, 30 jours retention
    "notifications-events" = "2:86400000"       # 2 partitions, 1 jour retention
    "course-enrollment-events" = "2:7776000000" # 2 partitions, 90 jours retention
    "chatbot-interaction-events" = "2:604800000" # 2 partitions, 7 jours retention
}

Write-Host "Creating Kafka topics..." -ForegroundColor Yellow
Write-Host ""

foreach ($topic in $topics.Keys) {
    $config = $topics[$topic]
    $partitions, $retention = $config -split ':'
    
    Write-Host "Creating topic: $topic" -ForegroundColor Blue
    Write-Host "  - Partitions: $partitions"
    Write-Host "  - Retention: $retention ms"
    
    $result = docker exec $KAFKA_CONTAINER kafka-topics --create `
        --bootstrap-server $BOOTSTRAP_SERVER `
        --topic $topic `
        --partitions $partitions `
        --replication-factor 1 `
        --if-not-exists `
        --config retention.ms=$retention `
        --config compression.type=lz4 `
        --config min.insync.replicas=1 2>&1

    if ($LASTEXITCODE -eq 0) {
        Write-Host "? Topic '$topic' created successfully" -ForegroundColor Green
    } else {
        Write-Host "? Topic '$topic' may already exist" -ForegroundColor Yellow
    }
    Write-Host ""
}

Write-Host "============================================" -ForegroundColor Blue
Write-Host "Listing all topics:" -ForegroundColor Yellow
Write-Host "============================================" -ForegroundColor Blue
docker exec $KAFKA_CONTAINER kafka-topics --list --bootstrap-server $BOOTSTRAP_SERVER
Write-Host ""

Write-Host "============================================" -ForegroundColor Blue
Write-Host "Topic details:" -ForegroundColor Yellow
Write-Host "============================================" -ForegroundColor Blue
foreach ($topic in $topics.Keys) {
    Write-Host ""
    Write-Host "=== $topic ===" -ForegroundColor Green
    docker exec $KAFKA_CONTAINER kafka-topics --describe `
        --bootstrap-server $BOOTSTRAP_SERVER `
        --topic $topic
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "? All topics created successfully!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""

# Instructions de test
Write-Host "To test the topics:" -ForegroundColor Yellow
Write-Host ""
Write-Host "# Produce a test message:" -ForegroundColor Blue
Write-Host "docker exec -it $KAFKA_CONTAINER kafka-console-producer \"
Write-Host "  --bootstrap-server $BOOTSTRAP_SERVER \"
Write-Host "  --topic user-activity-events"
Write-Host ""
Write-Host "# Consume messages:" -ForegroundColor Blue
Write-Host "docker exec $KAFKA_CONTAINER kafka-console-consumer \"
Write-Host "  --bootstrap-server $BOOTSTRAP_SERVER \"
Write-Host "  --topic user-activity-events \"
Write-Host "  --from-beginning"
Write-Host ""
