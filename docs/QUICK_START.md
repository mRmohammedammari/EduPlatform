# ?? Guide de Démarrage Rapide - EduPlatform

Suivez ces étapes pour démarrer rapidement avec EduPlatform.

---

## ? Démarrage en 5 minutes

### Étape 1: Cloner le projet

```bash
git clone https://github.com/votre-username/EduPlatform.git
cd EduPlatform
```

### Étape 2: Initialiser la plateforme

```powershell
# Exécuter le script d'initialisation automatique
.\scripts\init-platform.ps1
```

Ce script va:
- ? Vérifier les prérequis (Docker, .NET)
- ? Démarrer les services Docker (Cassandra, Kafka, Redis)
- ? Initialiser Cassandra et créer les tables
- ? Créer les topics Kafka
- ? Restaurer les dépendances .NET
- ? Appliquer les migrations SQL Server
- ? Builder le projet

### Étape 3: Configurer les secrets

```powershell
cd EduPlatform.API

# Configurer la clé OpenAI (pour le chatbot)
dotnet user-secrets set "OpenAI:ApiKey" "sk-votre-clé-openai"

# Configurer la clé JWT (minimum 32 caractères)
dotnet user-secrets set "Jwt:Key" "votre-super-secret-key-minimum-32-caracteres"
```

### Étape 4: Insérer les données de test (optionnel)

**SQL Server:**
```powershell
# Ouvrir SQL Server Management Studio et exécuter:
# scripts\sqlserver\seed-data.sql
```

**Cassandra:**
```bash
docker exec -i cassandra_edu cqlsh < scripts/cassandra/seed-data.cql
```

### Étape 5: Lancer l'application

**Terminal 1 - API:**
```powershell
cd EduPlatform.API
dotnet run
```

**Terminal 2 - Web:**
```powershell
cd EduPlatform.Web
dotnet run
```

### Étape 6: Accéder à l'application

- **Frontend:** https://localhost:7286
- **API:** https://localhost:7194
- **Swagger:** https://localhost:7194/swagger

---

## ?? Comptes de Test

Après avoir exécuté le script de seed data SQL Server:

| Rôle | Email | Mot de passe |
|------|-------|--------------|
| **Admin** | admin@eduplatform.com | Admin123! |
| **Instructeur** | instructor@eduplatform.com | Instructor123! |
| **Étudiant** | student@eduplatform.com | Student123! |

---

## ??? Commandes Utiles

### Docker

```bash
# Démarrer tous les services
docker-compose up -d

# Arrêter tous les services
docker-compose down

# Voir les logs
docker-compose logs -f

# Voir le statut
docker-compose ps

# Redémarrer un service spécifique
docker-compose restart cassandra
```

### Cassandra

```bash
# Se connecter à Cassandra
docker exec -it cassandra_edu cqlsh

# Vérifier les tables
docker exec cassandra_edu cqlsh -e "DESCRIBE KEYSPACES;"
docker exec cassandra_edu cqlsh -e "USE edu_platform; DESCRIBE TABLES;"
```

### Kafka

```bash
# Lister les topics
docker exec kafka_edu kafka-topics --list --bootstrap-server localhost:9092

# Voir les messages d'un topic
docker exec kafka_edu kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic user-activity-events \
  --from-beginning
```

### .NET

```bash
# Restaurer les dépendances
dotnet restore

# Builder le projet
dotnet build

# Exécuter les tests
dotnet test

# Créer une migration
dotnet ef migrations add MigrationName --project EduPlatform.Data --startup-project EduPlatform.API

# Appliquer les migrations
dotnet ef database update --project EduPlatform.Data --startup-project EduPlatform.API
```

---

## ?? Dépannage

### Problème: Cassandra ne démarre pas

```bash
# Voir les logs
docker-compose logs cassandra

# Redémarrer avec logs
docker-compose restart cassandra && docker-compose logs -f cassandra

# Vérifier l'espace disque
docker system df
```

### Problème: Kafka ne démarre pas

```bash
# Vérifier Zookeeper d'abord
docker-compose logs zookeeper

# Redémarrer dans le bon ordre
docker-compose restart zookeeper
sleep 10
docker-compose restart kafka
```

### Problème: Erreur de connexion SQL Server

1. Vérifier que SQL Server est installé et démarré
2. Vérifier la connection string dans `appsettings.json`
3. Tester la connexion:
   ```bash
   sqlcmd -S localhost\SQLEXPRESS -E -Q "SELECT @@VERSION"
   ```

### Problème: Port déjà utilisé

```bash
# Windows - Trouver le processus utilisant un port
netstat -ano | findstr :7194

# Tuer le processus
taskkill /PID <PID> /F
```

---

## ?? Prochaines Étapes

1. **Explorer la documentation:**
   - [Architecture](docs/ARCHITECTURE.md)
   - [API Documentation](docs/API_DOCUMENTATION.md)
   - [Roadmap](docs/ROADMAP.md)

2. **Commencer le développement:**
   - Voir [ROADMAP_STATUS.md](docs/ROADMAP_STATUS.md) pour savoir quoi implémenter ensuite

3. **Contribuer:**
   - Lire le guide de contribution
   - Créer une branche pour vos modifications
   - Soumettre une Pull Request

---

## ?? Objectifs de Développement

### Court terme (1-2 semaines)
- [ ] Compléter la Phase 1 de la roadmap
- [ ] Configurer l'authentification Blazor
- [ ] Mettre à jour le NavMenu

### Moyen terme (1 mois)
- [ ] Créer toutes les pages Web manquantes
- [ ] Implémenter les tests unitaires
- [ ] Déployer en staging

### Long terme (3 mois)
- [ ] Système de notifications
- [ ] Upload de vidéos
- [ ] Application mobile

---

## ?? Support

- **Documentation:** Consultez le dossier `docs/`
- **Issues:** Créez une issue sur GitHub
- **Email:** support@eduplatform.com

---

**Dernière mise à jour:** 2026-08-28
