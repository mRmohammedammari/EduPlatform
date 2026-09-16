-- ============================================
-- EduPlatform Cassandra Initialization Script
-- ============================================
-- Version: 1.0
-- Date: 2026-08-28
-- Description: Initialise le keyspace et les tables pour EduPlatform

-- ============================================
-- 1. CREATE KEYSPACE
-- ============================================

CREATE KEYSPACE IF NOT EXISTS edu_platform
WITH replication = {
  'class': 'SimpleStrategy',
  'replication_factor': 1
};

-- En production, utilisez NetworkTopologyStrategy:
-- CREATE KEYSPACE IF NOT EXISTS edu_platform
-- WITH replication = {
--   'class': 'NetworkTopologyStrategy',
--   'datacenter1': 3
-- };

-- Utiliser le keyspace
USE edu_platform;

-- ============================================
-- 2. USER ACTIVITIES TABLE
-- ============================================

CREATE TABLE IF NOT EXISTS user_activities (
    user_id uuid,
    course_id uuid,
    session_id uuid,
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

-- ============================================
-- 3. TEST RESULTS TABLE
-- ============================================

CREATE TABLE IF NOT EXISTS test_results (
    user_id uuid,
    course_id uuid,
    test_id uuid,
    submitted_at timestamp,
    score decimal,
    max_score decimal,
    percentage_score decimal,
    passed boolean,
    answers map<text, text>,
    time_taken_sec int,
    PRIMARY KEY ((user_id, course_id), submitted_at, test_id)
) WITH CLUSTERING ORDER BY (submitted_at DESC, test_id ASC)
  AND comment = 'Résultats des tests passés par les utilisateurs'
  AND gc_grace_seconds = 864000;

-- Index pour les analyses
CREATE INDEX IF NOT EXISTS idx_test_passed 
ON test_results (passed);

-- ============================================
-- 4. CHAT MESSAGES TABLE
-- ============================================

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
  AND default_time_to_live = 7776000;

-- Index pour recherche par cours
CREATE INDEX IF NOT EXISTS idx_chat_course 
ON chat_messages (course_id);

-- ============================================
-- 5. VERIFICATION
-- ============================================

-- Afficher les tables créées
DESCRIBE TABLES;

-- Afficher les détails de chaque table
DESCRIBE TABLE user_activities;
DESCRIBE TABLE test_results;
DESCRIBE TABLE chat_messages;

-- Compter les enregistrements (devrait être 0 après l'init)
SELECT COUNT(*) FROM user_activities;
SELECT COUNT(*) FROM test_results;
SELECT COUNT(*) FROM chat_messages;

-- ============================================
-- Script terminé avec succès
-- ============================================
