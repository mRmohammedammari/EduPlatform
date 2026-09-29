# ?? EduPlatform - Plateforme �ducative Intelligente

## ?? Description

EduPlatform est une plateforme d'apprentissage en ligne moderne propuls�e par le Big Data et l'Intelligence Artificielle. Elle offre une exp�rience personnalis�e gr�ce � des recommandations intelligentes, un chatbot p�dagogique et une analyse avanc�e des donn�es d'apprentissage.

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
- CSS personnalis�

**Big Data & Analytics**
- Apache Kafka (�v�nements temps r�el)
- Apache Cassandra (donn�es massives)
- Redis (cache)
- ML.NET (recommandations)

**Intelligence Artificielle**
- OpenAI GPT-4o-mini (chatbot)
- ML.NET (recommandations de cours)

**Base de donn�es**
- SQL Server (donn�es relationnelles)
- Cassandra (activit�s utilisateurs, r�sultats tests, historique chat)
- Redis (cache sessions, cours populaires)

## ?? Structure du Projet

```
EduPlatform/
??? EduPlatform.API/              # API REST principale
?   ??? Controllers/              # Endpoints API
?   ??? Services/                 # Services m�tier
?   ??? Program.cs               # Configuration
?
??? EduPlatform.Web/              # Interface Blazor
?   ??? Components/
?   ?   ??? Pages/               # Pages Razor
?   ?   ??? Layout/              # Layouts
?   ??? wwwroot/                 # Assets statiques
?
??? EduPlatform.Core/             # Mod�les domaine
?   ??? Models/                  # Entit�s m�tier
?   ??? Services/                # Services core
?
??? EduPlatform.Data/             # Acc�s aux donn�es
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

## ?? D�marrage Rapide

### Pr�requis

- .NET 8 SDK
- Docker Desktop
- Visual Studio 2022 ou VS Code

### Installation

#### ?? M�thode Rapide (Recommand�e)

```powershell
# 1. Cloner le repository
git clone https://github.com/votre-username/EduPlatform.git
cd EduPlatform

# 2. D�finir le mot de passe SQL Server Docker
$env:SQLSERVER_SA_PASSWORD = "Choisir-Un-MotDePasse!Sql2026"
$env:JWT_SECRET_KEY = "Choisir-Une-Cle-JWT-De-32-Caracteres-Minimum!"

# 3. Activer les donn�es de d�monstration si n�cessaire
$env:SEED_DEMO_DATA = "true"

# 4. Ex�cuter le script d'initialisation automatique
.\scripts\init-platform.ps1

# 5. Configurer les secrets
cd EduPlatform.API
dotnet user-secrets set "OpenAI:ApiKey" "votre-cl�-openai"
dotnet user-secrets set "Jwt:Key" "votre-cl�-jwt-32-caract�res-minimum"

# 6. La stack compl�te est maintenant d�marr�e

# Web: http://localhost:5297
# API: http://localhost:5053
```

Pour le mode d�veloppement sans Docker API/Web :

```powershell
# API
cd EduPlatform.API
dotnet run

# Web, dans un autre terminal
# Terminal 1:
cd EduPlatform.Web
dotnet run
```

**Voir le [Guide de D�marrage Rapide](docs/QUICK_START.md) pour plus de d�tails.**

#### ?? M�thode Manuelle

1. **D�marrer l'infrastructure Docker**
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
   # Docker recommand� : SQL Server est cr�� par docker-compose
   $env:SQLSERVER_SA_PASSWORD = "Choisir-Un-MotDePasse!Sql2026"
   docker compose up -d

   # Mode local Windows uniquement
   dotnet ef database update --project EduPlatform.Data --startup-project EduPlatform.API
   
   # Ins�rer les donn�es de test (optionnel)
   # Ex�cuter scripts/sqlserver/seed-data.sql dans SSMS
   ```

5. **Configurer les secrets**
   ```bash
   cd EduPlatform.API
   dotnet user-secrets set "OpenAI:ApiKey" "votre-cl�-openai"
   dotnet user-secrets set "Jwt:Key" "votre-cl�-jwt-32-caract�res-minimum"
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

7. **Acc�der � l'application**
   - Frontend: https://localhost:7286
   - API: https://localhost:7194
   - Swagger: https://localhost:7194/swagger

**Comptes de test** (apr�s seed data):
- Admin: admin@eduplatform.com / Admin123!
- Instructeur: instructor@eduplatform.com / Instructor123!
- �tudiant: student@eduplatform.com / Student123!

## ?? Documentation

### Pour Commencer
- ?? [**Guide de D�marrage Rapide**](docs/QUICK_START.md) - Commencez ici!
- ?? [**Roadmap**](docs/ROADMAP.md) - Plan de d�veloppement complet
- ?? [**Statut de la Roadmap**](docs/ROADMAP_STATUS.md) - Progression actuelle
- ?? [**Pistes d'amelioration UI/UX**](docs/IMPROVEMENTS.md) - Ecarts d'affichage identifies et priorises
- ?? [**Taches restantes**](docs/REMAINING_TASKS.md) - Ce qu'il reste a faire

### Architecture et Technique
- ??? [**Architecture D�taill�e**](docs/ARCHITECTURE.md)
- ?? [**API Documentation**](docs/API_DOCUMENTATION.md)
- [**Etat du projet et guide complet d'exploitation**](docs/PROJECT_OPERATIONS_GUIDE.md) - Architecture, API, bases de donnees, Docker, CI, Kubernetes et reste a faire
- ?? [**Guide de S�curit�**](docs/SECURITY.md)

### Infrastructure
- ??? [**Configuration Cassandra**](docs/CASSANDRA_SETUP.md)
- ? [**Configuration Kafka**](docs/KAFKA_SETUP.md)

### D�veloppement
- ?? [**Guide de Tests**](docs/TESTING.md)
- ?? [**Guide de D�ploiement**](docs/DEPLOYMENT.md)

## ?? Fonctionnalit�s Principales

### ? Impl�ment�es

- ? Authentification JWT
- ? Gestion des cours et modules
- ? Inscription aux cours
- ? Tests QCM
- ? Chatbot IA (OpenAI)
- ? Suivi des activit�s (Cassandra)
- ? �v�nements temps r�el (Kafka)
- ? Recommandations de cours (ML.NET)
- ? Cache Redis
- ? API REST compl�te
- ? Espace d'apprentissage texte et vid�o
- ? Progression et historique des sessions utilisateur
- ? Espace professeur et gestion des modules
- ? Gestion des questions QCM et analytics
- ? Dashboard administrateur et gestion des r�les
- ? Healthcheck des d�pendances
- ? D�ploiement Docker API/Web
- ? Notifications in-app
- ? Certificat de r�ussite consultable

### ?? En D�veloppement (voir ROADMAP.md)

- ?? Paiement reel *(gele - le paiement actuel est simule et ne doit pas servir en production)*
- ?? Couverture de code : 47 % mesures, objectif 80 %
- ?? Stockage objet / CDN et transcodage des medias
- ?? Certificat TLS de production (les certificats nginx actuels sont auto-signes)
- ?? Ameliorations d'affichage - voir [IMPROVEMENTS.md](docs/IMPROVEMENTS.md)

## ?? Tests

```bash
# Lancer tous les tests
dotnet test

# Tests unitaires
dotnet test --filter Category=Unit

# Tests d'int�gration
dotnet test --filter Category=Integration
```

## ?? S�curit�

- JWT pour l'authentification
- HTTPS obligatoire en production
- Validation des entr�es utilisateur
- Protection CORS configur�e
- Secrets g�r�s via User Secrets / Azure Key Vault

?? **Important**: Ne jamais commiter les cl�s API dans le code source!

## ?? Monitoring

- Health checks: `https://localhost:7194/health`
- Metrics: Configuration Prometheus disponible
- Logs: Serilog (� configurer)

## ?? Contribution

1. Fork le projet
2. Cr�er une branche (`git checkout -b feature/nouvelle-fonctionnalite`)
3. Commit les changements (`git commit -m 'Ajout nouvelle fonctionnalit�'`)
4. Push vers la branche (`git push origin feature/nouvelle-fonctionnalite`)
5. Ouvrir une Pull Request

## ?? Licence

Ce projet est sous licence MIT - voir le fichier [LICENSE](LICENSE) pour plus de d�tails.

## ?? Auteurs

- Votre Nom - D�veloppeur Principal

## ?? Remerciements

- OpenAI pour l'API GPT
- Apache Software Foundation (Kafka, Cassandra)
- Microsoft (.NET, ML.NET)
- La communaut� open source

## ?? Support

Pour toute question ou probl�me:
- Ouvrir une issue sur GitHub
- Email: support@eduplatform.com
- Documentation: https://docs.eduplatform.com
