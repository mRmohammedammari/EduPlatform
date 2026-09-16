# ?? Documentation API - EduPlatform

## Base URL
- **Development:** `https://localhost:7194/api`
- **Production:** `https://api.eduplatform.com/api`

## Authentication

Toutes les routes protégées requièrent un JWT Bearer token dans le header:

```http
Authorization: Bearer {your_jwt_token}
```

---

## ?? Authentication Endpoints

### POST /auth/register
Créer un nouveau compte utilisateur.

**Request Body:**
```json
{
  "email": "student@example.com",
  "password": "SecurePassword123!",
  "firstName": "John",
  "lastName": "Doe"
}
```

**Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "role": "Student"
}
```

**Errors:**
- `400 Bad Request`: Email déjà utilisé
- `422 Unprocessable Entity`: Validation échouée

---

### POST /auth/login
Authentifier un utilisateur existant.

**Request Body:**
```json
{
  "email": "student@example.com",
  "password": "SecurePassword123!"
}
```

**Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "firstName": "John",
  "role": "Student"
}
```

**Errors:**
- `401 Unauthorized`: Email ou mot de passe incorrect

---

## ?? Courses Endpoints

### GET /courses
Récupérer la liste des cours publiés.

**Query Parameters:**
- `category` (optional): Filtrer par catégorie (BigData, IA, Programmation, WebDev)
- `level` (optional): Filtrer par niveau (Débutant, Intermédiaire, Avancé)

**Response (200 OK):**
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "title": "Introduction au Big Data",
    "description": "Apprenez les fondamentaux du Big Data",
    "category": "BigData",
    "level": "Débutant",
    "durationMinutes": 360,
    "thumbnailUrl": "https://...",
    "instructorId": "...",
    "createdAt": "2026-08-01T10:00:00Z",
    "isPublished": true,
    "modules": [
      {
        "id": "...",
        "title": "Introduction",
        "videoUrl": "https://...",
        "durationMinutes": 45,
        "order": 1
      }
    ]
  }
]
```

---

### GET /courses/{id}
Récupérer les détails d'un cours spécifique.

**Path Parameters:**
- `id` (guid): ID du cours

**Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "title": "Introduction au Big Data",
  "description": "...",
  "category": "BigData",
  "level": "Débutant",
  "durationMinutes": 360,
  "modules": [ /* ... */ ],
  "enrollments": [ /* ... */ ]
}
```

**Errors:**
- `404 Not Found`: Cours inexistant

---

### POST /courses
Créer un nouveau cours (Instructeur/Admin uniquement).

**Authorization:** `Instructor` ou `Admin`

**Request Body:**
```json
{
  "title": "Nouveau cours",
  "description": "Description détaillée",
  "category": "Programmation",
  "level": "Intermédiaire",
  "durationMinutes": 480,
  "thumbnailUrl": "https://...",
  "instructorId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

**Response (201 Created):**
```json
{
  "id": "new-course-id",
  "title": "Nouveau cours",
  "createdAt": "2026-08-24T12:00:00Z",
  /* ... autres champs */
}
```

**Errors:**
- `401 Unauthorized`: Non authentifié
- `403 Forbidden`: Rôle insuffisant

---

### POST /courses/{id}/enroll
S'inscrire à un cours (authentification requise).

**Authorization:** Tout utilisateur authentifié

**Path Parameters:**
- `id` (guid): ID du cours

**Response (200 OK):**
```json
{
  "message": "Inscription réussie"
}
```

**Errors:**
- `400 Bad Request`: Déjà inscrit
- `401 Unauthorized`: Non authentifié
- `404 Not Found`: Cours inexistant

---

### GET /courses/{id}/enrollment-status
Vérifier si l'utilisateur est inscrit à un cours.

**Authorization:** Authentifié

**Response (200 OK):**
```json
{
  "isEnrolled": true,
  "enrolledAt": "2026-08-10T14:30:00Z",
  "progressPercent": 45
}
```

---

## ?? Tests Endpoints

### GET /tests/course/{courseId}
Récupérer les questions d'un test pour un cours.

**Authorization:** Authentifié et inscrit au cours

**Path Parameters:**
- `courseId` (guid): ID du cours

**Response (200 OK):**
```json
[
  {
    "id": "question-id",
    "text": "Qu'est-ce que Hadoop ?",
    "options": [
      "Un framework Big Data",
      "Un langage de programmation",
      "Une base de données",
      "Un système d'exploitation"
    ],
    "points": 1
  }
]
```

**Errors:**
- `403 Forbidden`: Non inscrit au cours

---

### POST /tests/submit
Soumettre les réponses à un test.

**Authorization:** Authentifié

**Request Body:**
```json
{
  "courseId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "answers": {
    "question-id-1": "Réponse A",
    "question-id-2": "Réponse C"
  }
}
```

**Response (200 OK):**
```json
{
  "score": 8.5,
  "maxScore": 10,
  "percentageScore": 85,
  "passed": true,
  "submittedAt": "2026-08-24T15:00:00Z"
}
```

---

### GET /tests/results/{courseId}
Récupérer les résultats de tests pour un cours.

**Authorization:** Authentifié

**Response (200 OK):**
```json
[
  {
    "testId": "...",
    "submittedAt": "2026-08-24T15:00:00Z",
    "score": 8.5,
    "maxScore": 10,
    "percentageScore": 85
  }
]
```

---

## ?? Chatbot Endpoints

### POST /chatbot/message
Envoyer un message au chatbot IA.

**Authorization:** Authentifié

**Request Body:**
```json
{
  "sessionId": "session-guid",
  "courseId": "course-guid (optional)",
  "message": "Qu'est-ce que MapReduce ?"
}
```

**Response (200 OK):**
```json
{
  "message": "MapReduce est un modèle de programmation...",
  "sessionId": "session-guid",
  "timestamp": "2026-08-24T15:30:00Z"
}
```

---

### GET /chatbot/history/{sessionId}
Récupérer l'historique d'une conversation.

**Authorization:** Authentifié

**Response (200 OK):**
```json
[
  {
    "role": "user",
    "content": "Qu'est-ce que MapReduce ?",
    "timestamp": "2026-08-24T15:30:00Z"
  },
  {
    "role": "assistant",
    "content": "MapReduce est...",
    "timestamp": "2026-08-24T15:30:05Z"
  }
]
```

---

## ?? Progress Endpoints

### POST /progress/log
Enregistrer une activité utilisateur.

**Authorization:** Authentifié

**Request Body:**
```json
{
  "courseId": "course-guid",
  "actionType": "video_view",
  "durationSec": 300,
  "pageUrl": "/courses/course-id/module/1",
  "deviceType": "web"
}
```

**Response (200 OK):**
```json
{
  "success": true
}
```

---

### GET /progress/{courseId}
Récupérer la progression sur un cours.

**Authorization:** Authentifié

**Response (200 OK):**
```json
{
  "totalTimeSeconds": 3600,
  "modulesCompleted": 5,
  "totalModules": 10,
  "progressPercent": 50,
  "lastActivity": "2026-08-24T14:00:00Z"
}
```

---

### GET /progress/history
Récupérer l'historique des activités.

**Authorization:** Authentifié

**Query Parameters:**
- `limit` (optional, default: 20): Nombre d'activités à récupérer

**Response (200 OK):**
```json
[
  {
    "courseId": "...",
    "actionType": "video_view",
    "durationSec": 300,
    "timestamp": "2026-08-24T14:00:00Z"
  }
]
```

---

## ?? Recommendations Endpoints

### GET /recommendations
Récupérer les cours recommandés pour l'utilisateur.

**Authorization:** Authentifié

**Query Parameters:**
- `limit` (optional, default: 5): Nombre de recommandations

**Response (200 OK):**
```json
[
  {
    "course": {
      "id": "...",
      "title": "Machine Learning Avancé",
      "category": "IA",
      "level": "Avancé"
    },
    "score": 0.92,
    "reason": "Basé sur votre intérêt pour l'IA et votre progression"
  }
]
```

---

## ? Error Responses

Tous les endpoints peuvent retourner ces erreurs standard:

### 400 Bad Request
```json
{
  "message": "Données invalides",
  "errors": {
    "email": ["L'email est requis"]
  }
}
```

### 401 Unauthorized
```json
{
  "message": "Token manquant ou invalide"
}
```

### 403 Forbidden
```json
{
  "message": "Accès refusé. Permissions insuffisantes."
}
```

### 404 Not Found
```json
{
  "message": "Ressource introuvable"
}
```

### 500 Internal Server Error
```json
{
  "message": "Une erreur interne s'est produite",
  "requestId": "unique-request-id"
}
```

---

## ?? Rate Limiting

**Limites (en développement):**
- Login: 5 tentatives / 5 minutes
- API générale: 100 requêtes / minute
- Chatbot: 20 messages / minute

**Headers de réponse:**
```http
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 85
X-RateLimit-Reset: 1672531200
```

---

## ?? Pagination

Les endpoints retournant des listes supportent la pagination:

**Query Parameters:**
- `page` (default: 1)
- `pageSize` (default: 20, max: 100)

**Response Headers:**
```http
X-Total-Count: 250
X-Page: 1
X-Page-Size: 20
X-Total-Pages: 13
```

---

## ?? Testing

### Swagger UI
Accédez à la documentation interactive:
- **Development:** https://localhost:7194/swagger

### Exemple avec cURL

```bash
# Register
curl -X POST https://localhost:7194/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test123!",
    "firstName": "Test",
    "lastName": "User"
  }'

# Login
curl -X POST https://localhost:7194/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test123!"
  }'

# Get courses (with JWT)
curl -X GET https://localhost:7194/api/courses \
  -H "Authorization: Bearer YOUR_TOKEN_HERE"
```

### Exemple avec JavaScript

```javascript
// Login
const loginResponse = await fetch('https://localhost:7194/api/auth/login', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    email: 'test@example.com',
    password: 'Test123!'
  })
});

const { token } = await loginResponse.json();

// Get courses
const coursesResponse = await fetch('https://localhost:7194/api/courses', {
  headers: { 'Authorization': `Bearer ${token}` }
});

const courses = await coursesResponse.json();
```

---

## ?? Resources

- [Swagger Documentation](https://localhost:7194/swagger)
- [Postman Collection](#) (à créer)
- [OpenAPI Spec](https://localhost:7194/swagger/v1/swagger.json)

**Dernière mise à jour:** 2026-08-24
