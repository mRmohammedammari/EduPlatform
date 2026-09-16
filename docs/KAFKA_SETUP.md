# ? Configuration Kafka - EduPlatform

## Overview

Apache Kafka est utilisé pour le streaming d'événements en temps réel:
- Événements d'activité utilisateur
- Résultats de tests
- Analytics en temps réel
- Notifications système

---

## ?? Installation et Démarrage

### Option 1: Docker Compose (Recommandé)

Le fichier `docker-compose.yml` inclut Kafka + Zookeeper:

```bash
# Démarrer Zookeeper et Kafka
docker-compose up -d zookeeper kafka

# Vérifier que les services sont démarrés
docker-compose ps

# Voir les logs
docker-compose logs kafka
docker-compose logs zookeeper
```

**Ordre de démarrage important:**
1. Zookeeper (démarre en premier)
2. Kafka (attend Zookeeper)

### Option 2: Installation locale

[Télécharger Apache Kafka](https://kafka.apache.org/downloads) (version 3.5+)

```bash
# Démarrer Zookeeper
bin/zookeeper-server-start.sh config/zookeeper.properties

# Démarrer Kafka (dans un autre terminal)
bin/kafka-server-start.sh config/server.properties
```

---

## ?? Configuration

### Connexion depuis l'application

**appsettings.json:**
```json
{
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "TopicActivity": "user-activity-events",
    "TopicTestResults": "test-results-events",
    "TopicNotifications": "notifications-events",
    "GroupId": "eduplatform-consumers"
  }
}
```

**Production:**
```json
{
  "Kafka": {
    "BootstrapServers": "kafka-broker-1:9092,kafka-broker-2:9092,kafka-broker-3:9092",
    "TopicActivity": "user-activity-events",
    "TopicTestResults": "test-results-events",
    "TopicNotifications": "notifications-events",
    "GroupId": "eduplatform-consumers",
    "SecurityProtocol": "SASL_SSL",
    "SaslMechanism": "PLAIN",
    "SaslUsername": "your-username",
    "SaslPassword": "your-password"
  }
}
```

---

## ?? Création des Topics

### Script d'initialisation

Créer `scripts/kafka/init-topics.sh`:

```bash
#!/bin/bash

KAFKA_CONTAINER="kafka_edu"
TOPICS=(
  "user-activity-events"
  "test-results-events"
  "notifications-events"
  "course-enrollment-events"
  "chatbot-interaction-events"
)

echo "Creating Kafka topics..."

for TOPIC in "${TOPICS[@]}"
do
  docker exec $KAFKA_CONTAINER kafka-topics --create \
    --bootstrap-server localhost:9092 \
    --topic $TOPIC \
    --partitions 3 \
    --replication-factor 1 \
    --if-not-exists \
    --config retention.ms=604800000 \
    --config compression.type=lz4

  if [ $? -eq 0 ]; then
    echo "? Topic '$TOPIC' created successfully"
  else
    echo "? Failed to create topic '$TOPIC'"
  fi
done

echo ""
echo "Listing all topics:"
docker exec $KAFKA_CONTAINER kafka-topics --list --bootstrap-server localhost:9092

echo ""
echo "Topic details:"
for TOPIC in "${TOPICS[@]}"
do
  echo ""
  echo "=== $TOPIC ==="
  docker exec $KAFKA_CONTAINER kafka-topics --describe \
    --bootstrap-server localhost:9092 \
    --topic $TOPIC
done
```

**Rendre le script exécutable et lancer:**

```bash
chmod +x scripts/kafka/init-topics.sh
./scripts/kafka/init-topics.sh
```

### Création manuelle des topics

```bash
# Topic pour les activités utilisateur
docker exec kafka_edu kafka-topics --create \
  --bootstrap-server localhost:9092 \
  --topic user-activity-events \
  --partitions 3 \
  --replication-factor 1 \
  --config retention.ms=604800000

# Topic pour les résultats de tests
docker exec kafka_edu kafka-topics --create \
  --bootstrap-server localhost:9092 \
  --topic test-results-events \
  --partitions 3 \
  --replication-factor 1 \
  --config retention.ms=2592000000

# Topic pour les notifications
docker exec kafka_edu kafka-topics --create \
  --bootstrap-server localhost:9092 \
  --topic notifications-events \
  --partitions 2 \
  --replication-factor 1 \
  --config retention.ms=86400000

# Topic pour les inscriptions aux cours
docker exec kafka_edu kafka-topics --create \
  --bootstrap-server localhost:9092 \
  --topic course-enrollment-events \
  --partitions 2 \
  --replication-factor 1

# Topic pour les interactions chatbot
docker exec kafka_edu kafka-topics --create \
  --bootstrap-server localhost:9092 \
  --topic chatbot-interaction-events \
  --partitions 2 \
  --replication-factor 1
```

---

## ?? Configuration des Topics

### Partitions

**Recommandations:**
- `user-activity-events`: 3-5 partitions (volume élevé)
- `test-results-events`: 3 partitions
- `notifications-events`: 2 partitions
- Autres topics: 1-2 partitions

**Formule:** Partitions = (Débit souhaité en MB/s) / (Débit par consumer en MB/s)

### Réplication Factor

- **Development:** 1
- **Staging:** 2
- **Production:** 3 (minimum)

### Retention

| Topic | Retention | Raison |
|-------|-----------|--------|
| user-activity-events | 7 jours | 604800000 ms |
| test-results-events | 30 jours | 2592000000 ms |
| notifications-events | 1 jour | 86400000 ms |
| course-enrollment-events | 90 jours | 7776000000 ms |

---

## ?? Vérification et Monitoring

### Lister les topics

```bash
docker exec kafka_edu kafka-topics --list --bootstrap-server localhost:9092
```

### Détails d'un topic

```bash
docker exec kafka_edu kafka-topics --describe \
  --bootstrap-server localhost:9092 \
  --topic user-activity-events
```

### Voir les messages d'un topic

```bash
# Tous les messages depuis le début
docker exec kafka_edu kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic user-activity-events \
  --from-beginning

# Uniquement les nouveaux messages
docker exec kafka_edu kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic user-activity-events

# Avec clés et timestamp
docker exec kafka_edu kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic user-activity-events \
  --property print.key=true \
  --property print.timestamp=true \
  --from-beginning
```

### Tester l'envoi de messages

```bash
# Producer interactif
docker exec -it kafka_edu kafka-console-producer \
  --bootstrap-server localhost:9092 \
  --topic user-activity-events

# Taper vos messages (un par ligne), Ctrl+C pour quitter
```

### Consumer Groups

```bash
# Lister les consumer groups
docker exec kafka_edu kafka-consumer-groups \
  --bootstrap-server localhost:9092 \
  --list

# Détails d'un consumer group
docker exec kafka_edu kafka-consumer-groups \
  --bootstrap-server localhost:9092 \
  --group eduplatform-consumers \
  --describe

# Voir le lag (retard de consommation)
docker exec kafka_edu kafka-consumer-groups \
  --bootstrap-server localhost:9092 \
  --group eduplatform-consumers \
  --describe \
  --members
```

---

## ?? Format des Messages

### user-activity-events

```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "courseId": "7b2c5f89-1234-5678-9abc-def012345678",
  "actionType": "video_view",
  "durationSec": 300,
  "pageUrl": "/courses/big-data/module-1",
  "deviceType": "web",
  "timestamp": "2026-08-24T15:30:00Z",
  "metadata": {
    "browser": "Chrome",
    "os": "Windows"
  }
}
```

**Action Types:**
- `video_view`: Visionnage de vidéo
- `quiz_attempt`: Tentative de quiz
- `module_complete`: Module terminé
- `course_start`: Début de cours
- `course_complete`: Cours terminé
- `resource_download`: Téléchargement de ressource

### test-results-events

```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "courseId": "7b2c5f89-1234-5678-9abc-def012345678",
  "testId": "abc12345-6789-0def-1234-567890abcdef",
  "score": 8.5,
  "maxScore": 10.0,
  "percentageScore": 85.0,
  "passed": true,
  "submittedAt": "2026-08-24T16:00:00Z",
  "timeTakenSec": 420,
  "answers": {
    "question1": "A",
    "question2": "C"
  }
}
```

### notifications-events

```json
{
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "type": "course_completion",
  "title": "Félicitations !",
  "message": "Vous avez terminé le cours 'Introduction au Big Data'",
  "actionUrl": "/courses/7b2c5f89-1234-5678-9abc-def012345678",
  "priority": "high",
  "timestamp": "2026-08-24T17:00:00Z",
  "metadata": {
    "courseId": "7b2c5f89-1234-5678-9abc-def012345678",
    "courseName": "Introduction au Big Data"
  }
}
```

---

## ?? Tests et Debugging

### Script de test complet

Créer `scripts/kafka/test-kafka.sh`:

```bash
#!/bin/bash

KAFKA_CONTAINER="kafka_edu"
TEST_TOPIC="test-topic"

echo "=== Kafka Test Script ==="
echo ""

# 1. Créer un topic de test
echo "1. Creating test topic..."
docker exec $KAFKA_CONTAINER kafka-topics --create \
  --bootstrap-server localhost:9092 \
  --topic $TEST_TOPIC \
  --partitions 1 \
  --replication-factor 1 \
  --if-not-exists

# 2. Envoyer des messages de test
echo ""
echo "2. Sending test messages..."
echo '{"test": "message1", "timestamp": "2026-08-24T10:00:00Z"}' | \
  docker exec -i $KAFKA_CONTAINER kafka-console-producer \
    --bootstrap-server localhost:9092 \
    --topic $TEST_TOPIC

echo '{"test": "message2", "timestamp": "2026-08-24T10:01:00Z"}' | \
  docker exec -i $KAFKA_CONTAINER kafka-console-producer \
    --bootstrap-server localhost:9092 \
    --topic $TEST_TOPIC

# 3. Consommer les messages
echo ""
echo "3. Consuming messages..."
docker exec $KAFKA_CONTAINER kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic $TEST_TOPIC \
  --from-beginning \
  --max-messages 2

# 4. Supprimer le topic de test
echo ""
echo "4. Cleaning up test topic..."
docker exec $KAFKA_CONTAINER kafka-topics --delete \
  --bootstrap-server localhost:9092 \
  --topic $TEST_TOPIC

echo ""
echo "=== Test Complete ==="
```

### Tester depuis l'application

**Producer Test (C#):**

```csharp
// Dans un controller ou service
[HttpPost("test-kafka")]
public async Task<IActionResult> TestKafka()
{
    var producer = HttpContext.RequestServices
        .GetRequiredService<EventProducer>();
    
    var testEvent = new UserActivityEvent
    {
        UserId = Guid.NewGuid(),
        CourseId = Guid.NewGuid(),
        ActionType = "test_event",
        DurationSec = 0,
        Timestamp = DateTime.UtcNow
    };
    
    await producer.PublishActivityAsync(testEvent);
    
    return Ok(new { message = "Event published to Kafka" });
}
```

---

## ??? Maintenance

### Augmenter les partitions

```bash
docker exec kafka_edu kafka-topics --alter \
  --bootstrap-server localhost:9092 \
  --topic user-activity-events \
  --partitions 5
```

?? **Note:** On ne peut qu'augmenter le nombre de partitions, jamais le diminuer.

### Modifier la retention

```bash
docker exec kafka_edu kafka-configs --alter \
  --bootstrap-server localhost:9092 \
  --entity-type topics \
  --entity-name user-activity-events \
  --add-config retention.ms=1209600000  # 14 jours
```

### Supprimer un topic

```bash
docker exec kafka_edu kafka-topics --delete \
  --bootstrap-server localhost:9092 \
  --topic old-topic-name
```

### Voir la taille d'un topic

```bash
docker exec kafka_edu kafka-log-dirs \
  --bootstrap-server localhost:9092 \
  --topic-list user-activity-events \
  --describe
```

### Reset un consumer group

```bash
# Arrêter tous les consumers du group d'abord!

docker exec kafka_edu kafka-consumer-groups \
  --bootstrap-server localhost:9092 \
  --group eduplatform-consumers \
  --reset-offsets \
  --to-earliest \
  --topic user-activity-events \
  --execute
```

---

## ?? Troubleshooting

### Problème: Kafka ne démarre pas

```bash
# Vérifier Zookeeper d'abord
docker-compose logs zookeeper

# Vérifier les logs Kafka
docker-compose logs kafka

# Redémarrer dans le bon ordre
docker-compose restart zookeeper
sleep 10
docker-compose restart kafka
```

### Problème: Cannot connect to Kafka

```bash
# Vérifier que le port est exposé
docker-compose ps

# Tester la connexion
telnet localhost 9092

# Vérifier la configuration
docker exec kafka_edu cat /etc/kafka/server.properties | grep advertised
```

### Problème: Consumer lag élevé

```bash
# Voir le lag
docker exec kafka_edu kafka-consumer-groups \
  --bootstrap-server localhost:9092 \
  --group eduplatform-consumers \
  --describe

# Solutions:
# - Augmenter le nombre de partitions
# - Augmenter le nombre de consumers
# - Optimiser le traitement dans le consumer
```

### Problème: Messages perdus

```bash
# Vérifier la configuration du producer
# Configurer acks=all pour garantir la durabilité

# Dans le code C#:
var config = new ProducerConfig
{
    BootstrapServers = "localhost:9092",
    Acks = Acks.All,  // Attendre confirmation de tous les replicas
    MessageSendMaxRetries = 3,
    EnableIdempotence = true
};
```

---

## ?? Monitoring en Production

### Métriques importantes

- **Throughput:** Messages/sec
- **Latency:** Temps de bout en bout
- **Consumer Lag:** Retard de consommation
- **Disk Usage:** Espace disque utilisé
- **Network I/O:** Bande passante réseau

### Outils de monitoring

- **Kafka Manager / CMAK:** UI pour gérer Kafka
- **Kafdrop:** Interface web pour explorer les topics
- **Prometheus + Grafana:** Monitoring avancé
- **Confluent Control Center:** Solution commerciale complète

### Installation Kafdrop (optionnel)

Ajouter à `docker-compose.yml`:

```yaml
  kafdrop:
    image: obsidiandynamics/kafdrop:latest
    container_name: kafdrop_edu
    ports:
      - "9000:9000"
    environment:
      KAFKA_BROKERCONNECT: kafka:9092
    depends_on:
      - kafka
```

Accès: http://localhost:9000

---

## ?? Ressources

- [Documentation Apache Kafka](https://kafka.apache.org/documentation/)
- [Confluent Kafka .NET Client](https://docs.confluent.io/kafka-clients/dotnet/current/overview.html)
- [Kafka Best Practices](https://kafka.apache.org/documentation/#bestpractices)
- [Kafka Definitive Guide](https://www.confluent.io/resources/kafka-the-definitive-guide/)

**Dernière mise à jour:** 2026-08-24
