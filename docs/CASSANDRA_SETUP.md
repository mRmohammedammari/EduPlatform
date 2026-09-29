# ??? Configuration Cassandra - EduPlatform

> **Schema de reference :** le fichier maintenu et applique par l'initialisation de la plateforme est [`../scripts/cassandra/init.cql`](../scripts/cassandra/init.cql). Les exemples de tables plus bas dans ce guide sont historiques; verifier le script avant de les executer manuellement.

## Overview

Cassandra est utilisé pour stocker les données volumineuses et time-series:
- Activités utilisateurs
- Résultats des tests
- Historique des conversations avec le chatbot

---

## ?? Installation et Démarrage

### Option 1: Docker (Recommandé pour développement)

Le fichier `docker-compose.yml` inclut déjà Cassandra:

```bash
# Démarrer Cassandra
docker-compose up -d cassandra

# Vérifier que Cassandra est démarré
docker-compose ps

# Voir les logs
docker-compose logs cassandra
```

**Attendre ~60 secondes** que Cassandra soit complètement démarré.

### Option 2: Installation locale

[Télécharger Apache Cassandra](https://cassandra.apache.org/download/) (version 4.1+)

```bash
# Linux/Mac
bin/cassandra

# Windows
bin\cassandra.bat
```

---

## ?? Configuration

### Connexion depuis l'application

**appsettings.json:**
```json
{
  "Cassandra": {
    "Host": "localhost",
    "Port": "9042",
    "Keyspace": "edu_platform",
    "Username": "",
    "Password": ""
  }
}
```

**Production (avec authentification):**
```json
{
  "Cassandra": {
    "Host": "cassandra-cluster.example.com",
    "Port": "9042",
    "Keyspace": "edu_platform",
    "Username": "admin",
    "Password": "your-secure-password"
  }
}
```

---

## ?? Initialisation du Keyspace et des Tables

### Étape 1: Accéder au shell CQL

```bash
# Via Docker
docker exec -it cassandra_edu cqlsh

# Installation locale
cqlsh localhost
```

### Étape 2: Créer le Keyspace

```cql
CREATE KEYSPACE IF NOT EXISTS edu_platform
WITH replication = {
  'class': 'SimpleStrategy',
  'replication_factor': 1
};

-- En production, utilisez NetworkTopologyStrategy:
-- CREATE KEYSPACE edu_platform
-- WITH replication = {
--   'class': 'NetworkTopologyStrategy',
--   'datacenter1': 3
-- };

USE edu_platform;
```

### Étape 3: Créer les Tables

#### Table user_activities

```cql
CREATE TABLE IF NOT EXISTS user_activities (
    user_id uuid,
    course_id uuid,
    timestamp timestamp,
    action_type text,
    duration_sec int,
    page_url text,
    device_type text,
    metadata map<text, text>,
    PRIMARY KEY ((user_id, course_id), timestamp)
) WITH CLUSTERING ORDER BY (timestamp DESC)
  AND comment = 'Stocke toutes les activités des utilisateurs sur les cours'
  AND gc_grace_seconds = 864000
  AND compaction = {
    'class': 'TimeWindowCompactionStrategy',
    'compaction_window_unit': 'DAYS',
    'compaction_window_size': 1
  };

-- Index pour recherche par type d'action
CREATE INDEX IF NOT EXISTS idx_action_type 
ON user_activities (action_type);
```

**Colonnes:**
- `user_id`: ID de l'utilisateur
- `course_id`: ID du cours
- `timestamp`: Date/heure de l'activité
- `action_type`: Type d'action (video_view, quiz_attempt, module_complete, etc.)
- `duration_sec`: Durée en secondes
- `page_url`: URL de la page
- `device_type`: web, mobile, tablet
- `metadata`: Données additionnelles (JSON)

**Exemples de requêtes:**
```cql
-- Toutes les activités d'un user sur un cours
SELECT * FROM user_activities 
WHERE user_id = ? AND course_id = ?
LIMIT 100;

-- Activités récentes
SELECT * FROM user_activities 
WHERE user_id = ? AND course_id = ?
AND timestamp > '2026-08-01'
LIMIT 50;

-- Temps total passé
SELECT SUM(duration_sec) as total_time
FROM user_activities 
WHERE user_id = ? AND course_id = ?;
```

---

#### Table test_results

```cql
CREATE TABLE IF NOT EXISTS test_results (
    course_id uuid,
  user_id uuid,
  taken_at timestamp,
    max_score decimal,
    passed boolean,
  score decimal,
    answers map<text, text>,
  PRIMARY KEY (course_id, user_id, taken_at)
) WITH CLUSTERING ORDER BY (user_id ASC, taken_at ASC)
  AND comment = 'Résultats des tests passés par les utilisateurs'
  AND gc_grace_seconds = 864000;

-- Index pour les analyses
CREATE INDEX IF NOT EXISTS idx_test_passed 
ON test_results (passed);
```

**Colonnes:**
- `user_id`: ID de l'utilisateur
- `course_id`: ID du cours
- `taken_at`: Date de soumission (colonne lue/ecrite par `TestResultRepository`)
- `score`: Score obtenu
- `max_score`: Score maximum possible
- `passed`: Test réussi ou non
- `answers`: Map des réponses (question_id ? réponse)

**Exemples de requêtes:**
```cql
-- Tous les résultats d'un user pour un cours
SELECT * FROM test_results 
WHERE course_id = ?;

-- Meilleur score
SELECT MAX(score) as best_score
FROM test_results 
WHERE course_id = ? AND user_id = ?;

-- Nombre de tentatives
SELECT COUNT(*) as attempts
FROM test_results 
WHERE course_id = ? AND user_id = ?;
```

---

#### Table chat_messages

```cql
CREATE TABLE IF NOT EXISTS chat_messages (
    user_id uuid,
    session_id uuid,
    timestamp timestamp,
    message_id uuid,
    role text,
    content text,
    course_id uuid,
    tokens_used int,
    PRIMARY KEY ((user_id, session_id), timestamp, message_id)
) WITH CLUSTERING ORDER BY (timestamp DESC, message_id ASC)
  AND comment = 'Historique des conversations avec le chatbot IA'
  AND gc_grace_seconds = 864000
  AND default_time_to_live = 7776000; -- 90 jours

-- Index pour recherche par cours
CREATE INDEX IF NOT EXISTS idx_chat_course 
ON chat_messages (course_id);
```

**Colonnes:**
- `user_id`: ID de l'utilisateur
- `session_id`: ID de la session de chat
- `timestamp`: Date/heure du message
- `message_id`: ID unique du message
- `role`: "user" ou "assistant"
- `content`: Contenu du message
- `course_id`: Cours associé (optionnel)
- `tokens_used`: Tokens consommés (pour facturation)

Le runtime `ChatRepository` n'utilise pas cette table historique. Il sauvegarde les sessions dans `chatbot_sessions`; cette table doit etre presente pour utiliser les routes chatbot.

```cql
CREATE TABLE IF NOT EXISTS chatbot_sessions (
  user_id uuid,
  session_id uuid,
  created_at timestamp,
  messages list<text>,
  PRIMARY KEY (user_id, session_id)
) WITH CLUSTERING ORDER BY (session_id ASC);
```

**Exemples de requêtes:**
```cql
-- Messages d'une session
SELECT * FROM chat_messages 
WHERE user_id = ? AND session_id = ?
ORDER BY timestamp ASC;

-- Messages récents d'un user
SELECT * FROM chat_messages 
WHERE user_id = ?
LIMIT 50;

-- Total tokens utilisés
SELECT SUM(tokens_used) as total_tokens
FROM chat_messages 
WHERE user_id = ? AND session_id = ?;
```

---

## ?? Vérification des Tables

```cql
-- Lister les keyspaces
DESCRIBE KEYSPACES;

-- Utiliser le keyspace
USE edu_platform;

-- Lister les tables
DESCRIBE TABLES;

-- Voir la structure d'une table
DESCRIBE TABLE user_activities;

-- Compter les enregistrements
SELECT COUNT(*) FROM user_activities;
SELECT COUNT(*) FROM test_results;
SELECT COUNT(*) FROM chat_messages;
SELECT COUNT(*) FROM chatbot_sessions;
```

---

## ?? Données de Test (Seed)

### Script d'insertion de données de test

```cql
-- Utiliser le keyspace
USE edu_platform;

-- Activités de test
INSERT INTO user_activities (
  user_id, course_id, timestamp, action_type, 
  duration_sec, page_url, device_type
) VALUES (
  11111111-1111-1111-1111-111111111111,
  22222222-2222-2222-2222-222222222222,
  toTimestamp(now()),
  'video_view',
  300,
  '/courses/big-data/module-1',
  'web'
);

INSERT INTO user_activities (
  user_id, course_id, timestamp, action_type, 
  duration_sec, page_url, device_type
) VALUES (
  11111111-1111-1111-1111-111111111111,
  22222222-2222-2222-2222-222222222222,
  toTimestamp(now()) - 1h,
  'quiz_attempt',
  180,
  '/courses/big-data/quiz-1',
  'web'
);

-- Résultats de test
INSERT INTO test_results (
  course_id, user_id, taken_at, score, max_score, passed, answers
) VALUES (
  22222222-2222-2222-2222-222222222222,
  11111111-1111-1111-1111-111111111111,
  toTimestamp(now()),
  8.5, 10.0, true,
  {'q1': 'A', 'q2': 'B'}
);

-- Messages de chat
INSERT INTO chat_messages (
  user_id, session_id, timestamp, message_id,
  role, content, course_id
) VALUES (
  11111111-1111-1111-1111-111111111111,
  44444444-4444-4444-4444-444444444444,
  toTimestamp(now()),
  uuid(),
  'user',
  'Qu''est-ce que Hadoop ?',
  22222222-2222-2222-2222-222222222222
);

INSERT INTO chat_messages (
  user_id, session_id, timestamp, message_id,
  role, content, course_id, tokens_used
) VALUES (
  11111111-1111-1111-1111-111111111111,
  44444444-4444-4444-4444-444444444444,
  toTimestamp(now()) + 2s,
  uuid(),
  'assistant',
  'Hadoop est un framework open-source pour le traitement distribué...',
  22222222-2222-2222-2222-222222222222,
  150
);
```

---

## ?? Script d'Initialisation Complet

Le schema executable est maintenu dans `scripts/cassandra/init.cql`. Ne pas recopier un schema parallele dans ce guide : cela a deja provoque un ecart entre `test_results` et `TestResultRepository`. Pour les tables des repositories, le script canonique declare `user_activities`, `test_results`, `chat_messages` et `chatbot_sessions`.

**Exécuter le script:**

```bash
# Windows PowerShell - depuis la racine du depot
Get-Content scripts/cassandra/init.cql | docker exec -i cassandra_edu cqlsh

# Installation locale - depuis la racine du depot
cqlsh -f scripts/cassandra/init.cql
```

Pour une base existante, `CREATE TABLE IF NOT EXISTS` ne modifie pas la structure d'une table deja creee. Comparer `DESCRIBE TABLE edu_platform.test_results` avec le script avant de mettre a jour. Une ancienne cle primaire necessite un export/import vers une nouvelle table; ne pas dropper la table en production.

---

## ??? Maintenance

### Backup

```bash
# Snapshot d'une table
docker exec cassandra_edu nodetool snapshot edu_platform -t backup_$(date +%Y%m%d)

# Voir les snapshots
docker exec cassandra_edu nodetool listsnapshots
```

### Restore

```bash
# Copier les fichiers de snapshot vers les dossiers de données
# Puis redémarrer Cassandra
docker-compose restart cassandra
```

### Monitoring

```bash
# Statut du cluster
docker exec cassandra_edu nodetool status

# Voir les métriques
docker exec cassandra_edu nodetool tablestats edu_platform

# Compaction status
docker exec cassandra_edu nodetool compactionstats
```

### Nettoyage

```bash
# Supprimer les anciennes données (tombstones)
docker exec cassandra_edu nodetool cleanup edu_platform

# Forcer la compaction
docker exec cassandra_edu nodetool compact edu_platform
```

---

## ?? Troubleshooting

### Problème: Cassandra ne démarre pas

```bash
# Voir les logs détaillés
docker-compose logs cassandra

# Vérifier l'espace disque
docker exec cassandra_edu df -h

# Redémarrer avec logs
docker-compose restart cassandra && docker-compose logs -f cassandra
```

### Problème: "Cannot connect to Cassandra"

```bash
# Vérifier que le port est bien exposé
docker-compose ps

# Tester la connexion
docker exec cassandra_edu cqlsh -e "DESCRIBE KEYSPACES;"

# Vérifier depuis l'application
telnet localhost 9042
```

### Problème: Performances lentes

```cql
-- Analyser les requêtes
TRACING ON;
SELECT * FROM user_activities WHERE user_id = ? AND course_id = ?;
TRACING OFF;

-- Vérifier la compaction
docker exec cassandra_edu nodetool compactionstats
```

---

## ?? Ressources

- [Documentation Cassandra](https://cassandra.apache.org/doc/latest/)
- [CQL Reference](https://cassandra.apache.org/doc/latest/cql/)
- [DataStax Driver C#](https://docs.datastax.com/en/developer/csharp-driver/latest/)
- [Data Modeling Best Practices](https://cassandra.apache.org/doc/latest/data_modeling/)

**Dernière mise à jour:** 2026-08-24
