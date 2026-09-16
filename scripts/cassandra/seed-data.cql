-- ============================================
-- EduPlatform Cassandra Seed Data
-- ============================================
-- Version: 1.0
-- Date: 2026-08-28
-- Description: Données de test pour le développement

USE edu_platform;

-- ============================================
-- IDs de test (à réutiliser)
-- ============================================
-- User ID:    11111111-1111-1111-1111-111111111111
-- Course ID:  22222222-2222-2222-2222-222222222222
-- Test ID:    33333333-3333-3333-3333-333333333333
-- Session ID: 44444444-4444-4444-4444-444444444444

-- ============================================
-- 1. USER ACTIVITIES
-- ============================================

-- Activité 1: Visionnage de vidéo
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

-- Activité 2: Tentative de quiz
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

-- Activité 3: Module complété
INSERT INTO user_activities (
  user_id, course_id, timestamp, action_type, 
  duration_sec, page_url, device_type, metadata
) VALUES (
  11111111-1111-1111-1111-111111111111,
  22222222-2222-2222-2222-222222222222,
  toTimestamp(now()) - 2h,
  'module_complete',
  45,
  '/courses/big-data/module-1',
  'web',
  {'module_id': '1', 'module_title': 'Introduction'}
);

-- Activité 4: Début de cours
INSERT INTO user_activities (
  user_id, course_id, timestamp, action_type, 
  duration_sec, page_url, device_type
) VALUES (
  11111111-1111-1111-1111-111111111111,
  22222222-2222-2222-2222-222222222222,
  toTimestamp(now()) - 24h,
  'course_start',
  0,
  '/courses/big-data',
  'web'
);

-- Activité 5: Téléchargement de ressource
INSERT INTO user_activities (
  user_id, course_id, timestamp, action_type, 
  duration_sec, page_url, device_type, metadata
) VALUES (
  11111111-1111-1111-1111-111111111111,
  22222222-2222-2222-2222-222222222222,
  toTimestamp(now()) - 3h,
  'resource_download',
  0,
  '/courses/big-data/resources',
  'web',
  {'resource_name': 'hadoop-guide.pdf', 'resource_size': '2.5MB'}
);

-- ============================================
-- 2. TEST RESULTS
-- ============================================

-- Résultat 1: Test réussi
INSERT INTO test_results (
  user_id, course_id, taken_at,
  score, max_score, passed, answers
) VALUES (
  11111111-1111-1111-1111-111111111111,
  22222222-2222-2222-2222-222222222222,
  toTimestamp(now()),
  8.5, 10.0, true,
  {'q1': 'A', 'q2': 'C', 'q3': 'B', 'q4': 'A', 'q5': 'D'}
);

-- Résultat 2: Premier test (échec)
INSERT INTO test_results (
  user_id, course_id, taken_at,
  score, max_score, passed, answers
) VALUES (
  11111111-1111-1111-1111-111111111111,
  22222222-2222-2222-2222-222222222222,
  toTimestamp(now()) - 2d,
  5.5, 10.0, false,
  {'q1': 'B', 'q2': 'C', 'q3': 'A', 'q4': 'A', 'q5': 'D'}
);

-- Résultat 3: Test avancé
INSERT INTO test_results (
  user_id, course_id, taken_at,
  score, max_score, passed, answers
) VALUES (
  11111111-1111-1111-1111-111111111111,
  22222222-2222-2222-2222-222222222222,
  toTimestamp(now()) - 1d,
  9.0, 10.0, true,
  {'q1': 'A', 'q2': 'B', 'q3': 'C', 'q4': 'D', 'q5': 'A'}
);

-- ============================================
-- 3. CHAT MESSAGES
-- ============================================

-- Session de chat 1: Question sur Hadoop
INSERT INTO chat_messages (
  user_id, session_id, timestamp, message_id,
  role, content, course_id
) VALUES (
  11111111-1111-1111-1111-111111111111,
  44444444-4444-4444-4444-444444444444,
  toTimestamp(now()) - 30m,
  uuid(),
  'user',
  'Qu''est-ce que Hadoop et à quoi sert-il ?',
  22222222-2222-2222-2222-222222222222
);

INSERT INTO chat_messages (
  user_id, session_id, timestamp, message_id,
  role, content, course_id, tokens_used
) VALUES (
  11111111-1111-1111-1111-111111111111,
  44444444-4444-4444-4444-444444444444,
  toTimestamp(now()) - 30m + 3s,
  uuid(),
  'assistant',
  'Hadoop est un framework open-source développé par Apache pour le traitement distribué de grandes quantités de données. Il est composé de deux modules principaux : HDFS (Hadoop Distributed File System) pour le stockage distribué et MapReduce pour le traitement parallèle des données.',
  22222222-2222-2222-2222-222222222222,
  150
);

-- Suite de la conversation
INSERT INTO chat_messages (
  user_id, session_id, timestamp, message_id,
  role, content, course_id
) VALUES (
  11111111-1111-1111-1111-111111111111,
  44444444-4444-4444-4444-444444444444,
  toTimestamp(now()) - 28m,
  uuid(),
  'user',
  'Quelle est la différence entre HDFS et un système de fichiers traditionnel ?',
  22222222-2222-2222-2222-222222222222
);

INSERT INTO chat_messages (
  user_id, session_id, timestamp, message_id,
  role, content, course_id, tokens_used
) VALUES (
  11111111-1111-1111-1111-111111111111,
  44444444-4444-4444-4444-444444444444,
  toTimestamp(now()) - 28m + 4s,
  uuid(),
  'assistant',
  'HDFS diffère d''un système de fichiers traditionnel sur plusieurs points : 1) Les données sont réparties sur plusieurs machines, 2) Chaque bloc de données est répliqué (par défaut 3 fois) pour assurer la tolérance aux pannes, 3) HDFS est optimisé pour les grandes lectures séquentielles plutôt que les accès aléatoires, 4) Il peut gérer des fichiers de plusieurs téraoctets.',
  22222222-2222-2222-2222-222222222222,
  180
);

-- Session de chat 2: Question sur MapReduce
INSERT INTO chat_messages (
  user_id, session_id, timestamp, message_id,
  role, content, course_id
) VALUES (
  11111111-1111-1111-1111-111111111111,
  55555555-5555-5555-5555-555555555555,
  toTimestamp(now()) - 1h,
  uuid(),
  'user',
  'Comment fonctionne MapReduce ?',
  22222222-2222-2222-2222-222222222222
);

INSERT INTO chat_messages (
  user_id, session_id, timestamp, message_id,
  role, content, course_id, tokens_used
) VALUES (
  11111111-1111-1111-1111-111111111111,
  55555555-5555-5555-5555-555555555555,
  toTimestamp(now()) - 1h + 5s,
  uuid(),
  'assistant',
  'MapReduce fonctionne en deux phases principales : 1) Map - Les données sont divisées en petits morceaux et traitées en parallèle. Chaque mapper produit des paires clé-valeur. 2) Reduce - Les résultats sont regroupés par clé et agrégés pour produire le résultat final. Entre les deux, il y a une phase de Shuffle qui organise et trie les données.',
  22222222-2222-2222-2222-222222222222,
  170
);

-- ============================================
-- 4. VERIFICATION DES DONNEES
-- ============================================

-- Compter les activités
SELECT COUNT(*) as total_activities FROM user_activities;

-- Compter les résultats de tests
SELECT COUNT(*) as total_test_results FROM test_results;

-- Compter les messages de chat
SELECT COUNT(*) as total_chat_messages FROM chat_messages;

-- Afficher quelques activités
SELECT user_id, course_id, action_type, timestamp 
FROM user_activities 
LIMIT 5;

-- Afficher les résultats de tests
SELECT user_id, score, max_score, passed, taken_at
FROM test_results
LIMIT 5;

-- Afficher les messages de chat
SELECT role, content, timestamp
FROM chat_messages
WHERE user_id = 11111111-1111-1111-1111-111111111111
  AND session_id = 44444444-4444-4444-4444-444444444444
LIMIT 10;

-- ============================================
-- Seed data terminé avec succès
-- ============================================
