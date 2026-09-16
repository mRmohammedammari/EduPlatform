# ??? Architecture EduPlatform

## Vue d'ensemble

EduPlatform suit une architecture en couches avec séparation des responsabilités, intégrant des technologies Big Data et IA pour une expérience d'apprentissage personnalisée.

---

## ?? Architecture Globale

```
???????????????????????????????????????????????????????????????
?                    Frontend (Blazor Server)                  ?
?                  https://localhost:7286                      ?
???????????????????????????????????????????????????????????????
                      ? HTTPS/WebSocket
                      ?
???????????????????????????????????????????????????????????????
?                    API REST (ASP.NET Core)                   ?
?                  https://localhost:7194                      ?
?                                                               ?
?  ????????????????  ????????????????  ????????????????      ?
?  ? Controllers  ?  ?  Services    ?  ?   Kafka      ?      ?
?  ?              ?  ?              ?  ?  Producer    ?      ?
?  ????????????????  ????????????????  ????????????????      ?
?         ?                 ?                 ?               ?
???????????????????????????????????????????????????????????????
          ?                 ?                 ?
          ?                 ?                 ?
    ??????????????????????????????????????????????????
    ?           Data Layer (EduPlatform.Data)        ?
    ?                                                 ?
    ?  ???????????????  ???????????????             ?
    ?  ? EF Core     ?  ? Cassandra   ?             ?
    ?  ? DbContext   ?  ? Repositories?             ?
    ?  ???????????????  ???????????????             ?
    ?        ?                ?                      ?
    ??????????????????????????????????????????????????
             ?                ?
    ???????????????????  ?????????????????
    ?   SQL Server    ?  ?   Cassandra   ?
    ?  (Relationnel)  ?  ?  (NoSQL Big)  ?
    ???????????????????  ?????????????????

    ????????????????????????????????????????????????
    ?         Infrastructure Services               ?
    ?                                               ?
    ?  ????????  ????????  ????????  ??????????? ?
    ?  ?Redis ?  ?Kafka ?  ?OpenAI?  ? ML.NET  ? ?
    ?  ?Cache ?  ?Event ?  ?  API ?  ? Engine  ? ?
    ?  ????????  ????????  ????????  ??????????? ?
    ????????????????????????????????????????????????
```

---

## ?? Architecture en Couches

### 1. **Présentation Layer** (EduPlatform.Web)

**Technologie:** Blazor Server avec .NET 8

**Responsabilités:**
- Interface utilisateur interactive
- Gestion de l'état UI
- Communication avec l'API
- Validation côté client

**Composants clés:**
```
EduPlatform.Web/
??? Components/
?   ??? Pages/           # Pages Razor (@page)
?   ??? Layout/          # Layouts principaux
?   ??? Shared/          # Composants réutilisables
??? Services/            # Services frontend (Auth, HTTP)
??? wwwroot/            # Assets statiques
```

**Communication:**
- HTTP Client vers API REST
- SignalR pour temps réel (notifications)
- WebSocket pour Blazor Server

---

### 2. **API Layer** (EduPlatform.API)

**Technologie:** ASP.NET Core Web API 8.0

**Responsabilités:**
- Endpoints REST
- Authentification/Autorisation JWT
- Validation des requêtes
- Orchestration des services
- Gestion des erreurs

**Structure:**
```
EduPlatform.API/
??? Controllers/
?   ??? AuthController.cs           # Login, Register
?   ??? CoursesController.cs        # CRUD Cours
?   ??? TestsController.cs          # Tests QCM
?   ??? ChatbotController.cs        # IA Chatbot
?   ??? ProgressController.cs       # Suivi activité
?   ??? RecommendationsController.cs # Recommandations ML
??? Services/
?   ??? ChatbotService.cs           # Service OpenAI
??? Middleware/
?   ??? ExceptionMiddleware.cs      # Gestion erreurs globale
??? Program.cs                      # Configuration
```

**Sécurité:**
- JWT Bearer Authentication
- Role-based Authorization
- CORS configuré pour Blazor
- HTTPS obligatoire

---

### 3. **Business Logic Layer** (EduPlatform.Core)

**Responsabilités:**
- Modèles de domaine
- Logique métier
- Services core
- DTOs et validations

**Structure:**
```
EduPlatform.Core/
??? Models/
?   ??? User.cs              # Entité utilisateur
?   ??? Course.cs            # Entité cours
?   ??? Module.cs            # Entité module
?   ??? Enrollment.cs        # Inscription
?   ??? Question.cs          # Question QCM
?   ??? TestResult.cs        # Résultat test
?   ??? UserActivityEvent.cs # Événement activité
??? Services/
    ??? AuthService.cs       # Authentification, hash password
```

**Modèle de domaine:**
```
User (1) ???? (N) Enrollment (N) ???? (1) Course
                                           ?
                                           ???? (N) Module
                                           ???? (N) Question
```

---

### 4. **Data Access Layer** (EduPlatform.Data)

**Responsabilités:**
- Abstraction de la persistance
- Repositories pattern
- Migrations de base de données
- Caching

**Structure:**
```
EduPlatform.Data/
??? SqlServer/
?   ??? EduDbContext.cs              # EF Core DbContext
?   ??? Configurations/              # Fluent API configs
??? Cassandra/
?   ??? CassandraContext.cs          # Session Cassandra
?   ??? Repositories/
?       ??? ActivityRepository.cs    # Activités utilisateurs
?       ??? TestResultRepository.cs  # Résultats tests
?       ??? ChatRepository.cs        # Historique chat
??? Cache/
?   ??? CacheService.cs              # Service Redis
??? Migrations/                      # EF Core migrations
```

**Stratégie de données:**

| Type de données | Stockage | Raison |
|----------------|----------|--------|
| Utilisateurs, Cours, Inscriptions | SQL Server | Données relationnelles, transactions ACID |
| Activités utilisateur | Cassandra | Volume élevé, écriture intensive, time-series |
| Résultats de tests | Cassandra | Analyse Big Data, rapports historiques |
| Historique chat | Cassandra | Conversations volumineuses |
| Cache (cours populaires) | Redis | Accès ultra-rapide, TTL automatique |
| Sessions utilisateur | Redis | Temporaire, expiration |

---

### 5. **Big Data & Analytics Layer** (EduPlatform.BigData)

**Responsabilités:**
- Traitement événements temps réel
- Analytics et insights
- Recommandations ML
- Event streaming

**Structure:**
```
EduPlatform.BigData/
??? Kafka/
?   ??? EventProducer.cs      # Publier événements
?   ??? EventConsumer.cs      # Consommer et traiter
??? Analytics/
?   ??? AnalyticsService.cs   # Calculs statistiques
?   ??? RecommendationService.cs # ML.NET recommandations
??? Models/
    ??? AnalyticsModels.cs    # Modèles ML
```

**Pipeline événements:**
```
User Action ? API Controller ? Kafka Producer ? Topic
                                                  ?
                                            Consumer
                                                  ?
                                    ?????????????????????????????
                                    ?                           ?
                            Cassandra Storage          Analytics Processing
                                                              ?
                                                      ML Model Training
                                                              ?
                                                     Recommendations
```

---

## ?? Flux de Données Principaux

### Flux 1: Inscription à un cours

```
1. User clique "S'inscrire" (Blazor)
   ?
2. HTTP POST /api/courses/{id}/enroll (API)
   ?
3. Vérification authentification JWT
   ?
4. CoursesController.Enroll()
   ?
5. Création Enrollment dans SQL Server (EF Core)
   ?
6. Publish événement "CourseEnrolled" ? Kafka
   ?
7. EventConsumer ? Cassandra (analytics)
   ?
8. Invalidation cache Redis
   ?
9. Retour 200 OK
   ?
10. Blazor redirige vers le cours
```

### Flux 2: Chat avec IA

```
1. User envoie message (Blazor)
   ?
2. HTTP POST /api/chatbot/message (API)
   ?
3. ChatbotController.SendMessage()
   ?
4. Récupération historique ? ChatRepository (Cassandra)
   ?
5. Appel OpenAI API (ChatbotService)
   ?
6. Sauvegarde message + réponse ? Cassandra
   ?
7. Publish événement "ChatInteraction" ? Kafka
   ?
8. Retour réponse IA au client
```

### Flux 3: Recommandations personnalisées

```
1. User demande recommandations
   ?
2. HTTP GET /api/recommendations (API)
   ?
3. RecommendationService.GetRecommendations()
   ?
4. Récupération activités user ? Cassandra
   ?
5. Calcul similarité avec ML.NET
   ?
6. Récupération cours correspondants ? SQL Server
   ?
7. Cache résultats ? Redis (15 min)
   ?
8. Retour liste de cours recommandés
```

---

## ??? Schémas de Base de Données

### SQL Server (EduDbContext)

**Tables principales:**

```sql
Users
??? Id (PK, GUID)
??? Email (Unique)
??? PasswordHash
??? FirstName
??? LastName
??? Role (Student/Instructor/Admin)
??? CreatedAt

Courses
??? Id (PK, GUID)
??? Title
??? Description
??? Category
??? Level
??? DurationMinutes
??? InstructorId (FK ? Users)
??? IsPublished
??? CreatedAt

Modules
??? Id (PK, GUID)
??? Title
??? VideoUrl
??? DurationMinutes
??? Order
??? CourseId (FK ? Courses)

Enrollments
??? UserId (PK, FK ? Users)
??? CourseId (PK, FK ? Courses)
??? EnrolledAt
??? CompletedAt
??? ProgressPercent

Questions
??? Id (PK, GUID)
??? Text
??? Options (JSON)
??? CorrectAnswer
??? Points
??? CourseId (FK ? Courses)
```

### Cassandra (edu_platform keyspace)

**Tables time-series:**

```cql
-- Activités utilisateur
user_activities {
  user_id uuid,
  course_id uuid,
  timestamp timestamp,
  action_type text,
  duration_sec int,
  device_type text,
  PRIMARY KEY ((user_id, course_id), timestamp)
} WITH CLUSTERING ORDER BY (timestamp DESC);

-- Résultats des tests
test_results {
  user_id uuid,
  course_id uuid,
  test_id uuid,
  submitted_at timestamp,
  score decimal,
  answers map<text, text>,
  PRIMARY KEY ((user_id, course_id), submitted_at)
} WITH CLUSTERING ORDER BY (submitted_at DESC);

-- Historique chat
chat_messages {
  user_id uuid,
  session_id uuid,
  timestamp timestamp,
  role text,
  content text,
  PRIMARY KEY ((user_id, session_id), timestamp)
} WITH CLUSTERING ORDER BY (timestamp DESC);
```

### Redis (Structures de cache)

```
# Cours individuels
cache:course:{courseId} ? JSON du cours (TTL: 1h)

# Liste des cours
cache:courses:all ? Liste JSON (TTL: 15min)

# Recommandations
cache:recommendations:{userId} ? Liste courses (TTL: 15min)

# Sessions utilisateur
session:{sessionId} ? User info (TTL: 24h)
```

---

## ?? Sécurité

### Authentification & Autorisation

**JWT (JSON Web Token):**
```json
{
  "sub": "user-guid",
  "email": "user@example.com",
  "role": "Student",
  "exp": 1672531200,
  "iss": "EduPlatform",
  "aud": "EduPlatformUsers"
}
```

**Flow:**
1. Login ? Génération JWT
2. Client stocke token (localStorage)
3. Requêtes ? Header `Authorization: Bearer {token}`
4. API valide token à chaque requête
5. Expiration ? Refresh token / Re-login

**Autorization Policies:**
```csharp
[Authorize(Roles = "Student")]          // Tous étudiants
[Authorize(Roles = "Instructor,Admin")] // Instructeur OU Admin
[Authorize]                             // Tout utilisateur authentifié
```

### Protection des données

- **Passwords:** BCrypt hashing (WorkFactor: 12)
- **HTTPS:** Obligatoire en production
- **CORS:** Whitelist des origins Blazor
- **Secrets:** Azure Key Vault / User Secrets
- **SQL Injection:** Protection EF Core (paramétrisé)
- **XSS:** Blazor encode automatiquement

---

## ?? Performance & Scalabilité

### Stratégies de Cache

1. **Cache-Aside Pattern:**
   - Check cache ? Si absent ? DB ? Populate cache

2. **Invalidation:**
   - Lors de CREATE/UPDATE/DELETE

3. **TTL (Time To Live):**
   - Courses: 1h
   - Recommendations: 15min
   - Lists: 5min

### Optimisations Base de Données

**SQL Server:**
- Index sur Email (Users)
- Index sur CourseId (Modules)
- Pagination systématique
- Requêtes optimisées EF Core (AsNoTracking)

**Cassandra:**
- Partition key: user_id + course_id
- Clustering key: timestamp DESC
- Compaction strategy: TimeWindow

### Scalabilité Horizontale

**API:**
- Stateless ? Peut scaler horizontalement
- Load balancer devant plusieurs instances

**Kafka:**
- Partitions multiples par topic
- Consumer groups pour parallélisme

**Cassandra:**
- Cluster multi-nœuds
- Réplication factor: 3 (production)

---

## ?? Observabilité

### Logs
- **Serilog** (structured logging)
- Niveaux: Debug, Info, Warning, Error, Fatal
- Sinks: Console, File, Seq, Application Insights

### Métriques
- Requests/sec
- Response times (p50, p95, p99)
- Error rates
- Database query times

### Health Checks
- `/health` endpoint
- Checks: SQL Server, Cassandra, Redis, Kafka

---

## ?? Déploiement

### Environnements

| Environnement | URL | Base de données |
|---------------|-----|-----------------|
| Development | localhost:7194 | Local SQL Express, Docker |
| Staging | staging.eduplatform.com | Azure SQL, Managed Cassandra |
| Production | api.eduplatform.com | Azure SQL, Production Cluster |

### Container Strategy

**Docker Compose (Dev):**
- Cassandra
- Kafka + Zookeeper
- Redis
- (API et Web en local)

**Production:**
- API: Azure App Service / AWS ECS
- Web: Azure App Service
- Databases: Managed services

---

## ?? Évolutions Futures

### Short-term
- Microservices (si croissance)
- Message queue (RabbitMQ) pour tâches async
- CDN pour vidéos

### Long-term
- GraphQL API (alternative à REST)
- Event Sourcing pour audit complet
- CQRS pattern pour séparation lecture/écriture
- Elasticsearch pour recherche avancée

---

## ?? Références

- [ASP.NET Core Best Practices](https://learn.microsoft.com/en-us/aspnet/core/)
- [Cassandra Data Modeling](https://cassandra.apache.org/doc/latest/data_modeling/)
- [Kafka Documentation](https://kafka.apache.org/documentation/)
- [ML.NET Tutorials](https://dotnet.microsoft.com/learn/ml-dotnet)

**Dernière mise à jour:** 2026-08-24
