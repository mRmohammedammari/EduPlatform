# ?? EduPlatform - Plateforme Éducative Intelligente

## ?? Description

EduPlatform est une plateforme d'apprentissage en ligne moderne propulsée par le Big Data et l'Intelligence Artificielle. Elle offre une expérience personnalisée grâce à des recommandations intelligentes, un chatbot pédagogique et une analyse avancée des données d'apprentissage.

## ??? Architecture

### Stack Technologique

**Backend**
- ASP.NET Core 8.0 Web API
- Entity Framework Core
- JWT Authentication
- Swagger/OpenAPI

**Frontend**
- Blazor Server (.NET 8)
- Bootstrap 5
- CSS personnalisé

**Big Data & Analytics**
- Apache Kafka (événements temps réel)
- Apache Cassandra (données massives)
- Redis (cache)
- ML.NET (recommandations)

**Intelligence Artificielle**
- OpenAI GPT-4o-mini (chatbot)
- ML.NET (recommandations de cours)

**Base de données**
- SQL Server (données relationnelles)
- Cassandra (activités utilisateurs, résultats tests, historique chat)
- Redis (cache sessions, cours populaires)

## ?? Structure du Projet

```
EduPlatform/
??? EduPlatform.API/              # API REST principale
?   ??? Controllers/              # Endpoints API
?   ??? Services/                 # Services métier
?   ??? Program.cs               # Configuration
?
??? EduPlatform.Web/              # Interface Blazor
?   ??? Components/
?   ?   ??? Pages/               # Pages Razor
?   ?   ??? Layout/              # Layouts
?   ??? wwwroot/                 # Assets statiques
?
??? EduPlatform.Core/             # Modèles domaine
?   ??? Models/                  # Entités métier
?   ??? Services/                # Services core
?
??? EduPlatform.Data/             # Accès aux données
?   ??? SqlServer/               # EF Core DbContext
?   ??? Cassandra/               # Repositories Cassandra
?   ??? Cache/                   # Service Redis
?
??? EduPlatform.BigData/          # Traitement Big Data
?   ??? Kafka/                   # Producers/Consumers
?   ??? Analytics/               # Services analytics
?
??? docker-compose.yml            # Infrastructure locale
```

## ?? Démarrage Rapide

### Prérequis

- .NET 8 SDK
- Docker Desktop
- Visual Studio 2022 ou VS Code

### Installation

#### ?? Méthode Rapide (Recommandée)

```powershell
# 1. Cloner le repository
git clone https://github.com/votre-username/EduPlatform.git
cd EduPlatform

# 2. Définir le mot de passe SQL Server Docker
$env:SQLSERVER_SA_PASSWORD = "Choisir-Un-MotDePasse!Sql2026"
$env:JWT_SECRET_KEY = "Choisir-Une-Cle-JWT-De-32-Caracteres-Minimum!"

# 3. Activer les données de démonstration si nécessaire
$env:SEED_DEMO_DATA = "true"

# 4. Exécuter le script d'initialisation automatique
.\scripts\init-platform.ps1

# 5. Configurer les secrets
cd EduPlatform.API
dotnet user-secrets set "OpenAI:ApiKey" "votre-clé-openai"
dotnet user-secrets set "Jwt:Key" "votre-clé-jwt-32-caractères-minimum"

# 6. La stack complète est maintenant démarrée

# Web: http://localhost:5297
# API: http://localhost:5053
```

Pour le mode développement sans Docker API/Web :

```powershell
# API
cd EduPlatform.API
dotnet run

# Web, dans un autre terminal
# Terminal 1:
cd EduPlatform.Web
dotnet run
```

**Voir le [Guide de Démarrage Rapide](docs/QUICK_START.md) pour plus de détails.**

#### ?? Méthode Manuelle

1. **Démarrer l'infrastructure Docker**
   ```bash
   docker-compose up -d
   ```

2. **Initialiser Cassandra**
   ```bash
   docker exec -i cassandra_edu cqlsh < scripts/cassandra/init.cql
   docker exec -i cassandra_edu cqlsh < scripts/cassandra/seed-data.cql
   ```

3. **Initialiser Kafka**
   ```bash
   # Windows PowerShell
   .\scripts\kafka\init-topics.ps1
   
   # Linux/Mac
   ./scripts/kafka/init-topics.sh
   ```

4. **Configurer SQL Server**
   ```bash
   # Docker recommandé : SQL Server est créé par docker-compose
   $env:SQLSERVER_SA_PASSWORD = "Choisir-Un-MotDePasse!Sql2026"
   docker compose up -d

   # Mode local Windows uniquement
   dotnet ef database update --project EduPlatform.Data --startup-project EduPlatform.API
   
   # Insérer les données de test (optionnel)
   # Exécuter scripts/sqlserver/seed-data.sql dans SSMS
   ```

5. **Configurer les secrets**
   ```bash
   cd EduPlatform.API
   dotnet user-secrets set "OpenAI:ApiKey" "votre-clé-openai"
   dotnet user-secrets set "Jwt:Key" "votre-clé-jwt-32-caractères-minimum"
   ```

6. **Lancer l'application**
   
   Terminal 1 - API:
   ```bash
   cd EduPlatform.API
   dotnet run
   ```
   
   Terminal 2 - Web:
   ```bash
   cd EduPlatform.Web
   dotnet run
   ```

7. **Accéder à l'application**
   - Frontend: https://localhost:7286
   - API: https://localhost:7194
   - Swagger: https://localhost:7194/swagger

**Comptes de test** (après seed data):
- Admin: admin@eduplatform.com / Admin123!
- Instructeur: instructor@eduplatform.com / Instructor123!
- Étudiant: student@eduplatform.com / Student123!

## ?? Documentation

### Pour Commencer
- ?? [**Guide de Démarrage Rapide**](docs/QUICK_START.md) - Commencez ici!
- ?? [**Roadmap**](docs/ROADMAP.md) - Plan de développement complet
- ?? [**Statut de la Roadmap**](docs/ROADMAP_STATUS.md) - Progression actuelle

### Architecture et Technique
- ??? [**Architecture Détaillée**](docs/ARCHITECTURE.md)
- ?? [**API Documentation**](docs/API_DOCUMENTATION.md)
- ?? [**Guide de Sécurité**](docs/SECURITY.md)

### Infrastructure
- ??? [**Configuration Cassandra**](docs/CASSANDRA_SETUP.md)
- ? [**Configuration Kafka**](docs/KAFKA_SETUP.md)

### Développement
- ?? [**Guide de Tests**](docs/TESTING.md)
- ?? [**Guide de Déploiement**](docs/DEPLOYMENT.md)

## ?? Fonctionnalités Principales

### ? Implémentées

- ? Authentification JWT
- ? Gestion des cours et modules
- ? Inscription aux cours
- ? Tests QCM
- ? Chatbot IA (OpenAI)
- ? Suivi des activités (Cassandra)
- ? Événements temps réel (Kafka)
- ? Recommandations de cours (ML.NET)
- ? Cache Redis
- ? API REST complète
- ? Espace d'apprentissage texte et vidéo
- ? Progression et historique des sessions utilisateur
- ? Espace professeur et gestion des modules
- ? Gestion des questions QCM et analytics
- ? Dashboard administrateur et gestion des rôles
- ? Healthcheck des dépendances
- ? Déploiement Docker API/Web
- ? Notifications in-app
- ? Certificat de réussite consultable

### ?? En Développement (voir ROADMAP.md)

- ?? Paiement réel
- ?? Tests d'intégration complets

## ?? Tests

```bash
# Lancer tous les tests
dotnet test

# Tests unitaires
dotnet test --filter Category=Unit

# Tests d'intégration
dotnet test --filter Category=Integration
```

## ?? Sécurité

- JWT pour l'authentification
- HTTPS obligatoire en production
- Validation des entrées utilisateur
- Protection CORS configurée
- Secrets gérés via User Secrets / Azure Key Vault

?? **Important**: Ne jamais commiter les clés API dans le code source!

## ?? Monitoring

- Health checks: `https://localhost:7194/health`
- Metrics: Configuration Prometheus disponible
- Logs: Serilog (à configurer)

## ?? Contribution

1. Fork le projet
2. Créer une branche (`git checkout -b feature/nouvelle-fonctionnalite`)
3. Commit les changements (`git commit -m 'Ajout nouvelle fonctionnalité'`)
4. Push vers la branche (`git push origin feature/nouvelle-fonctionnalite`)
5. Ouvrir une Pull Request

## ?? Licence

Ce projet est sous licence MIT - voir le fichier [LICENSE](LICENSE) pour plus de détails.

## ?? Auteurs

- Votre Nom - Développeur Principal

## ?? Remerciements

- OpenAI pour l'API GPT
- Apache Software Foundation (Kafka, Cassandra)
- Microsoft (.NET, ML.NET)
- La communauté open source

## ?? Support

Pour toute question ou problème:
- Ouvrir une issue sur GitHub
- Email: support@eduplatform.com
- Documentation: https://docs.eduplatform.com
