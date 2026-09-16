#!/bin/bash

# ============================================
# EduPlatform Kafka Topics Initialization
# ============================================
# Version: 1.0
# Date: 2026-08-28
# Description: Crée tous les topics Kafka nécessaires

set -e  # Exit on error

KAFKA_CONTAINER="kafka_edu"
BOOTSTRAP_SERVER="localhost:9092"

# Couleurs pour les messages
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}============================================${NC}"
echo -e "${BLUE}  EduPlatform Kafka Topics Initialization${NC}"
echo -e "${BLUE}============================================${NC}"
echo ""

# Vérifier que Kafka est démarré
echo -e "${YELLOW}Checking if Kafka is running...${NC}"
if ! docker ps | grep -q $KAFKA_CONTAINER; then
    echo -e "${RED}Error: Kafka container is not running${NC}"
    echo "Please start Kafka with: docker-compose up -d kafka"
    exit 1
fi
echo -e "${GREEN}? Kafka is running${NC}"
echo ""

# Configuration des topics
declare -A TOPICS=(
    ["user-activity-events"]="3:604800000"      # 3 partitions, 7 jours retention
    ["test-results-events"]="3:2592000000"      # 3 partitions, 30 jours retention
    ["notifications-events"]="2:86400000"       # 2 partitions, 1 jour retention
    ["course-enrollment-events"]="2:7776000000" # 2 partitions, 90 jours retention
    ["chatbot-interaction-events"]="2:604800000" # 2 partitions, 7 jours retention
)

echo -e "${YELLOW}Creating Kafka topics...${NC}"
echo ""

for TOPIC in "${!TOPICS[@]}"
do
    IFS=':' read -r PARTITIONS RETENTION <<< "${TOPICS[$TOPIC]}"
    
    echo -e "${BLUE}Creating topic: ${TOPIC}${NC}"
    echo "  - Partitions: ${PARTITIONS}"
    echo "  - Retention: ${RETENTION}ms"
    
    docker exec $KAFKA_CONTAINER kafka-topics --create \
        --bootstrap-server $BOOTSTRAP_SERVER \
        --topic $TOPIC \
        --partitions $PARTITIONS \
        --replication-factor 1 \
        --if-not-exists \
        --config retention.ms=$RETENTION \
        --config compression.type=lz4 \
        --config min.insync.replicas=1 2>/dev/null

    if [ $? -eq 0 ]; then
        echo -e "${GREEN}? Topic '${TOPIC}' created successfully${NC}"
    else
        echo -e "${YELLOW}? Topic '${TOPIC}' may already exist${NC}"
    fi
    echo ""
done

echo -e "${BLUE}============================================${NC}"
echo -e "${YELLOW}Listing all topics:${NC}"
echo -e "${BLUE}============================================${NC}"
docker exec $KAFKA_CONTAINER kafka-topics --list --bootstrap-server $BOOTSTRAP_SERVER
echo ""

echo -e "${BLUE}============================================${NC}"
echo -e "${YELLOW}Topic details:${NC}"
echo -e "${BLUE}============================================${NC}"
for TOPIC in "${!TOPICS[@]}"
do
    echo ""
    echo -e "${GREEN}=== ${TOPIC} ===${NC}"
    docker exec $KAFKA_CONTAINER kafka-topics --describe \
        --bootstrap-server $BOOTSTRAP_SERVER \
        --topic $TOPIC
done

echo ""
echo -e "${GREEN}============================================${NC}"
echo -e "${GREEN}? All topics created successfully!${NC}"
echo -e "${GREEN}============================================${NC}"
echo ""

# Instructions de test
echo -e "${YELLOW}To test the topics:${NC}"
echo ""
echo -e "${BLUE}# Produce a test message:${NC}"
echo "docker exec -it $KAFKA_CONTAINER kafka-console-producer \\"
echo "  --bootstrap-server $BOOTSTRAP_SERVER \\"
echo "  --topic user-activity-events"
echo ""
echo -e "${BLUE}# Consume messages:${NC}"
echo "docker exec $KAFKA_CONTAINER kafka-console-consumer \\"
echo "  --bootstrap-server $BOOTSTRAP_SERVER \\"
echo "  --topic user-activity-events \\"
echo "  --from-beginning"
echo ""
