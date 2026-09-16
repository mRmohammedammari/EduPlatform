# ?? Guide de Démarrage Rapide - EduPlatform

## ? État Actuel du Projet

### Services Opérationnels
- ? **API Backend** : http://localhost:5053
- ? **Swagger UI** : http://localhost:5053/swagger
- ? **Docker Services** :
  - Cassandra : port 9042
  - Kafka : port 9092
  - Zookeeper : port 2181
  - Redis : port 6379

### Services à Démarrer
- ? **Web Frontend (Blazor)** : http://localhost:5297 en Docker ou https://localhost:7286 en local
- ?? **Base de données SQL Server** : vérifier les migrations avant la première utilisation

---

## ?? Démarrage Rapide

### 1. Démarrer l'infrastructure Docker

```powershell
cd C:\Projects\EduPlatform
docker compose up -d
```

Pour Docker, définir le mot de passe SQL Server avant le démarrage :

```powershell
$env:SQLSERVER_SA_PASSWORD = "Choisir-Un-MotDePasse!Sql2026"
$env:JWT_SECRET_KEY = "Choisir-Une-Cle-JWT-De-32-Caracteres-Minimum!"
docker compose up -d
```

Si Docker n'est pas disponible, démarrez Docker Desktop et attendez que son moteur Linux soit prêt avant de relancer la commande.

Pour charger les données de démonstration pendant l'initialisation complète :

```powershell
$env:SEED_DEMO_DATA = "true"
.\scripts\init-platform.ps1
```

Vérifier rapidement l'installation complète :

```powershell
.\scripts\smoke-test.ps1
```

Surveiller les dépendances et déclencher une alerte webhook optionnelle :

```powershell
.\scripts\monitor-health.ps1 -Checks 10 -IntervalSeconds 30
```

### 2. Démarrer l'API Backend

Pour démarrer API et Web dans Docker :

```powershell
docker compose up -d
```

- Web : http://localhost:5297
- API : http://localhost:5053
- Health détaillé : http://localhost:5053/health/details
- Après toute modification de l'interface Web, reconstruire l'image avec `docker compose build web` avant `docker compose up -d web`.
- Avant le premier démarrage, appliquer les migrations SQL Server avec `dotnet ef database update --project EduPlatform.Data --startup-project EduPlatform.API`.

> En Docker, SQL Server est fourni par le service `sqlserver_edu` et l'API utilise `Server=sqlserver,1433`. Le mode local Windows `SQLEXPRESS` reste disponible séparément.

Pour lancer l'API directement avec le SDK :

```powershell
cd C:\Projects\EduPlatform\EduPlatform.API
dotnet run
```

L'API sera accessible sur :
- **HTTP** : http://localhost:5053
- **Swagger** : http://localhost:5053/swagger

### 3. Démarrer le Frontend Blazor

```powershell
cd C:\Projects\EduPlatform\EduPlatform.Web
dotnet run --launch-profile https
```

Le site sera accessible sur :
- **HTTPS** : https://localhost:7286
- **HTTP** : http://localhost:5297

---

## ?? Configuration Requise

### Secrets Utilisateur (optionnel)

Pour activer le chatbot OpenAI, configurez les secrets :

```powershell
cd C:\Projects\EduPlatform\EduPlatform.API

# Clé OpenAI (remplacer par votre vraie clé)
dotnet user-secrets set "OpenAI:ApiKey" "sk-votre-cle-api-openai"

# Clé JWT (déjà configurée par défaut dans appsettings.json)
# dotnet user-secrets set "Jwt:Key" "votre-cle-jwt-32-caracteres-minimum"
```

### Base de Données SQL Server

Créer et initialiser la base de données :

```powershell
cd C:\Projects\EduPlatform\EduPlatform.API
dotnet ef database update
```

Sauvegarder la base :

```powershell
.\scripts\sqlserver\backup-database.ps1
```

Restaurer une sauvegarde :

```powershell
.\scripts\sqlserver\restore-database.ps1 -BackupFile ".\backups\EduPlatformDB-20260915-120000.bak"
```

---

## ?? Problèmes Connus et Solutions

### ? Consumer Kafka Actif

Le consumer Kafka est actif. Le démarrage est asynchrone et le broker doit être healthy avant de lancer l'API en Docker.

### ?? Port 5053 Déjà Utilisé

Si vous obtenez l'erreur `address already in use` :

```powershell
# Trouver le processus qui utilise le port
netstat -ano | findstr :5053

# Tuer le processus (remplacer PID par le numéro trouvé)
taskkill /PID <PID> /F
```

### ?? Kafka Topics Manquants

Si l'API ne démarre pas à cause de topics Kafka manquants :

```powershell
cd C:\Projects\EduPlatform
.\scripts\kafka\init-topics.ps1
```

---

## ?? Prochaines Étapes

### Phase 1 : Finaliser les Fondations (En cours)

- [x] Infrastructure Docker opérationnelle
- [x] API Backend fonctionnelle
- [x] Kafka topics créés
- [x] Cassandra initialisé
- [ ] Migrations SQL Server exécutées
- [ ] Frontend Blazor démarré et testé
- [x] Consumer Kafka réactivé et corrigé
- [ ] Données de test insérées

### Phase 2 : Authentification Frontend (À venir)

Voir [docs/ROADMAP.md](docs/ROADMAP.md) pour le plan complet.

---

## ?? Tests de l'API

### Via Swagger UI

Ouvrir http://localhost:5053/swagger dans le navigateur.

### Via cURL

```powershell
# Lister les cours
curl http://localhost:5053/api/courses

# Inscription utilisateur
curl -X POST http://localhost:5053/api/auth/register `
  -H "Content-Type: application/json" `
  -d '{"email":"test@example.com","password":"Test123!","name":"Test User"}'

# Connexion
curl -X POST http://localhost:5053/api/auth/login `
  -H "Content-Type: application/json" `
  -d '{"email":"test@example.com","password":"Test123!"}'
```

---

## ?? Documentation Complète

- [README.md](README.md) - Vue d'ensemble du projet
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) - Architecture technique
- [docs/ROADMAP.md](docs/ROADMAP.md) - Plan de développement
- [docs/QUICK_START.md](docs/QUICK_START.md) - Guide de démarrage détaillé
- [docs/API_GUIDE.md](docs/API_GUIDE.md) - Documentation API
- [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) - Guide de déploiement

---

## ?? Support

En cas de problème :

1. Vérifier que Docker est démarré : `docker ps`

Pour préparer un déploiement public, voir [docs/PRODUCTION_READINESS.md](docs/PRODUCTION_READINESS.md).
2. Vérifier les logs de l'API dans le terminal
3. Vérifier les logs Docker : `docker compose logs -f`
4. Consulter [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md)

---

**Dernière mise à jour** : 2026-08-28  
**Statut** : API Backend ? Opérationnelle | Frontend ? À démarrer
