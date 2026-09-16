# ?? Guide de Dépannage - EduPlatform

## ?? Table des Matières

1. [Problèmes de Démarrage](#problèmes-de-démarrage)
2. [Problèmes Docker](#problèmes-docker)
3. [Problèmes Kafka](#problèmes-kafka)
4. [Problèmes Cassandra](#problèmes-cassandra)
5. [Problèmes SQL Server](#problèmes-sql-server)
6. [Problèmes API](#problèmes-api)
7. [Problèmes Frontend](#problèmes-frontend)

---

## ?? Problèmes de Démarrage

### Port 5053 Déjà Utilisé

**Erreur** :
```
Failed to bind to address http://127.0.0.1:5053: address already in use
System.Net.Sockets.SocketException (10048)
```

**Diagnostic** :
```powershell
# Trouver quel processus utilise le port 5053
netstat -ano | findstr :5053
```

**Solution** :
```powershell
# Tuer le processus (remplacer <PID> par le numéro trouvé)
taskkill /PID <PID> /F

# OU changer le port dans launchSettings.json
# EduPlatform.API/Properties/launchSettings.json
```

---

### Port 7194 (HTTPS) Déjà Utilisé

**Diagnostic** :
```powershell
netstat -ano | findstr :7194
```

**Solution** :
```powershell
# Tuer le processus
taskkill /PID <PID> /F

# OU lancer avec le profil HTTP uniquement
dotnet run --launch-profile http
```

---

### Processus API Bloqué

**Erreur** :
```
The process cannot access the file 'EduPlatform.API.exe' because it is being used by another process
```

**Solution** :
```powershell
# Tuer tous les processus EduPlatform.API
Get-Process -Name "EduPlatform.API" -ErrorAction SilentlyContinue | Stop-Process -Force

# Nettoyer les fichiers binaires
cd C:\Projects\EduPlatform\EduPlatform.API
dotnet clean
dotnet build
```

---

## ?? Problèmes Docker

### Docker Services Non Démarrés

**Erreur** :
```
Error response from daemon: No such container: kafka_edu
```

**Diagnostic** :
```powershell
# Vérifier les conteneurs actifs
docker ps

# Vérifier tous les conteneurs (y compris arrêtés)
docker ps -a
```

**Solution** :
```powershell
cd C:\Projects\EduPlatform

# Démarrer tous les services
docker compose up -d

# Vérifier les logs
docker compose logs -f

# Redémarrer un service spécifique
docker compose restart kafka
```

---

### Docker Compose Version Obsolète

**Warning** :
```
the attribute `version` is obsolete, it will be ignored
```

**Solution** : Supprimer la ligne `version: "3.8"` dans `docker-compose.yml` (c'est juste un warning, pas critique).

---

### Conteneur Kafka Arrêté

**Diagnostic** :
```powershell
docker logs kafka_edu --tail 50
```

Si vous voyez `[KafkaServer id=1] shut down completed`, Kafka s'est arrêté.

**Solution** :
```powershell
# Redémarrer Kafka et Zookeeper
docker compose restart kafka zookeeper

# Attendre 30 secondes que Kafka soit prêt
Start-Sleep -Seconds 30

# Vérifier que Kafka répond
docker exec kafka_edu kafka-topics --bootstrap-server localhost:9092 --list
```

---

## ?? Problèmes Kafka

### Topics Manquants

**Erreur** :
```
Confluent.Kafka.ConsumeException: Subscribed topic not available: user-activity-events
Broker: Unknown topic or partition
```

**Diagnostic** :
```powershell
# Lister les topics existants
docker exec kafka_edu kafka-topics --bootstrap-server localhost:9092 --list
```

**Solution** :
```powershell
# Créer les topics avec le script fourni
cd C:\Projects\EduPlatform
.\scripts\kafka\init-topics.ps1
```

Topics requis :
- `user-activity-events` (3 partitions)
- `test-results-events` (3 partitions)
- `notifications-events` (2 partitions)
- `course-enrollment-events` (2 partitions)
- `chatbot-interaction-events` (2 partitions)

---

### Consumer Kafka Timeout

**Erreur** :
```
Consumer group session timed out (in join-state steady) after 171690 ms
%3|FAIL|rdkafka#consumer-1| localhost:9092/bootstrap: Connect to ipv4#127.0.0.1:9092 failed
```

**Cause** : Le consumer Kafka démarre avant que Kafka soit complètement prêt.

**Solution Temporaire** : Le consumer a été désactivé dans `Program.cs` :

```csharp
// Temporairement désactivé - problème de timeout Kafka consumer
//builder.Services.AddHostedService<EduPlatform.BigData.Kafka.ActivityConsumer>();
```

**Solution Permanente** (À implémenter) :

Modifier `EduPlatform.BigData/Kafka/EventConsumer.cs` :

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    // Attendre que Kafka soit prêt
    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
    
    var maxRetries = 5;
    var retryDelay = TimeSpan.FromSeconds(5);
    
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            _consumer.Subscribe("user-activity-events");
            _logger.LogInformation("[Kafka] Consumer démarré...");
            break;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Kafka] Tentative {Retry}/{Max} échouée", i + 1, maxRetries);
            if (i == maxRetries - 1) throw;
            await Task.Delay(retryDelay, stoppingToken);
        }
    }
    
    // ... reste du code
}
```

---

### Connexion Kafka Refusée

**Erreur** :
```
localhost:9092/bootstrap: Connect to ipv4#127.0.0.1:9092 failed: Unknown error
```

**Diagnostic** :
```powershell
# Vérifier que Kafka écoute sur le port 9092
netstat -ano | findstr :9092

# Vérifier les logs Kafka
docker logs kafka_edu --tail 100
```

**Solution** :
```powershell
# Redémarrer Kafka et Zookeeper dans l'ordre
docker compose stop kafka zookeeper
docker compose start zookeeper
Start-Sleep -Seconds 10
docker compose start kafka
Start-Sleep -Seconds 30

# Vérifier que Kafka fonctionne
docker exec kafka_edu kafka-broker-api-versions --bootstrap-server localhost:9092
```

---

## ??? Problèmes Cassandra

### Connexion Cassandra Échouée

**Erreur** :
```
Cassandra.NoHostAvailableException: All host tried for query failed
```

**Diagnostic** :
```powershell
# Vérifier que Cassandra est démarré
docker ps | findstr cassandra

# Vérifier les logs
docker logs cassandra_edu --tail 50

# Tester la connexion
docker exec cassandra_edu cqlsh -e "SELECT cluster_name FROM system.local;"
```

**Solution** :
```powershell
# Redémarrer Cassandra
docker compose restart cassandra

# Attendre que Cassandra soit prêt (peut prendre 1-2 minutes)
Start-Sleep -Seconds 60

# Vérifier le statut
docker exec cassandra_edu nodetool status
```

---

### Keyspace `edu_platform` Manquant

**Erreur** :
```
Keyspace 'edu_platform' does not exist
```

**Diagnostic** :
```powershell
# Lister les keyspaces
docker exec cassandra_edu cqlsh -e "DESCRIBE KEYSPACES;"
```

**Solution** :
```powershell
# Initialiser Cassandra avec le script
docker exec -i cassandra_edu cqlsh < C:\Projects\EduPlatform\scripts\cassandra\init.cql

# Vérifier les tables créées
docker exec cassandra_edu cqlsh -e "USE edu_platform; DESCRIBE TABLES;"
```

Tables attendues :
- `user_activity`
- `test_results`
- `chat_messages`

---

## ??? Problèmes SQL Server

### SQL Server Non Accessible

**Erreur** :
```
A network-related or instance-specific error occurred while establishing a connection to SQL Server
```

**Diagnostic** :
```powershell
# Vérifier que SQL Server est démarré
Get-Service -Name MSSQL*

# Tester la connexion
sqlcmd -S localhost\SQLEXPRESS -Q "SELECT @@VERSION"
```

**Solution** :
```powershell
# Démarrer SQL Server Express
net start MSSQL$SQLEXPRESS

# Si SQL Server n'est pas installé, installer SQL Server Express
# Télécharger depuis : https://www.microsoft.com/sql-server/sql-server-downloads
```

---

### Base de Données Non Créée

**Erreur** :
```
Cannot open database "EduPlatformDB" requested by the login
```

**Solution** :
```powershell
cd C:\Projects\EduPlatform\EduPlatform.API

# Créer la base de données via Entity Framework
dotnet ef database update

# Vérifier les migrations
dotnet ef migrations list
```

---

### Migrations Entity Framework Échouées

**Erreur** :
```
dotnet ef : The term 'dotnet ef' is not recognized
```

**Solution** :
```powershell
# Installer l'outil EF Core
dotnet tool install --global dotnet-ef

# OU mettre à jour
dotnet tool update --global dotnet-ef

# Réessayer
dotnet ef database update
```

---

## ?? Problèmes API

### API Ne Démarre Pas

**Erreur Générique** :
```
Hosting failed to start
```

**Diagnostic** :
```powershell
cd C:\Projects\EduPlatform\EduPlatform.API

# Voir les erreurs détaillées
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --verbosity detailed
```

**Checklist** :
1. ? Docker services démarrés (`docker ps`)
2. ? Ports libres (5053, 7194)
3. ? SQL Server accessible
4. ? Cassandra accessible
5. ? Kafka accessible
6. ? Redis accessible

---

### Erreur 401 Unauthorized

**Cause** : Token JWT manquant ou invalide.

**Solution** :
```powershell
# 1. Inscription
$registerBody = @{
    email = "test@eduplatform.com"
    password = "Test123!"
    name = "Test User"
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "http://localhost:5053/api/auth/register" `
    -Method POST `
    -ContentType "application/json" `
    -Body $registerBody

# 2. Connexion
$loginBody = @{
    email = "test@eduplatform.com"
    password = "Test123!"
} | ConvertTo-Json

$authResponse = Invoke-RestMethod -Uri "http://localhost:5053/api/auth/login" `
    -Method POST `
    -ContentType "application/json" `
    -Body $loginBody

$token = $authResponse.token

# 3. Utiliser le token
$headers = @{ Authorization = "Bearer $token" }
Invoke-RestMethod -Uri "http://localhost:5053/api/courses" -Headers $headers
```

---

### Swagger Non Accessible

**Erreur** : Page blanche ou 404 sur `/swagger`

**Solution** :
```powershell
# Vérifier que Swagger est activé dans Program.cs
# Les lignes suivantes doivent être présentes :

# app.UseSwagger();
# app.UseSwaggerUI();

# Accéder à l'URL complète
Start-Process "http://localhost:5053/swagger/index.html"
```

---

## ?? Problèmes Frontend

### Frontend Blazor Ne Démarre Pas

**Diagnostic** :
```powershell
cd C:\Projects\EduPlatform\EduPlatform.Web
dotnet run
```

**Erreurs Courantes** :

1. **Port 7286 déjà utilisé** :
```powershell
netstat -ano | findstr :7286
taskkill /PID <PID> /F
```

2. **Dépendances manquantes** :
```powershell
dotnet restore
dotnet build
```

---

### CORS Error

**Erreur** :
```
Access to fetch at 'http://localhost:5053/api/courses' from origin 'https://localhost:7286' has been blocked by CORS policy
```

**Vérification** : Dans `EduPlatform.API/Program.cs`, vérifier :

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazor", policy =>
    {
        policy.WithOrigins(
            "https://localhost:7286",  // Frontend HTTPS
            "http://localhost:5297",   // Frontend HTTP
            "https://localhost:7194",  // API HTTPS
            "http://localhost:5053")   // API HTTP
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ...

app.UseCors("AllowBlazor");
```

---

## ?? Commandes de Diagnostic Utiles

### Vérifier Tous les Services

```powershell
# Docker
docker ps

# Ports API
netstat -ano | findstr ":5053 :7194"

# Ports Frontend
netstat -ano | findstr ":7286 :5297"

# Ports Infrastructure
netstat -ano | findstr ":9042 :9092 :6379 :2181"

# SQL Server
Get-Service MSSQL* | Select-Object Name, Status

# Processus .NET
Get-Process -Name "EduPlatform.*"
```

---

### Vérifier Kafka Complet

```powershell
# Status du conteneur
docker ps | findstr kafka

# Topics
docker exec kafka_edu kafka-topics --bootstrap-server localhost:9092 --list

# Consumer groups
docker exec kafka_edu kafka-consumer-groups --bootstrap-server localhost:9092 --list

# Logs
docker logs kafka_edu --tail 100
```

---

### Vérifier Cassandra Complet

```powershell
# Status
docker exec cassandra_edu nodetool status

# Keyspaces
docker exec cassandra_edu cqlsh -e "DESCRIBE KEYSPACES;"

# Tables
docker exec cassandra_edu cqlsh -e "USE edu_platform; DESCRIBE TABLES;"

# Compter les enregistrements
docker exec cassandra_edu cqlsh -e "USE edu_platform; SELECT COUNT(*) FROM user_activity;"
```

---

## ?? Réinitialisation Complète

Si rien ne fonctionne, réinitialiser complètement :

```powershell
# 1. Arrêter tous les processus .NET
Get-Process -Name "EduPlatform.*" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Stop-Process -Force

# 2. Arrêter et supprimer tous les conteneurs Docker
cd C:\Projects\EduPlatform
docker compose down -v

# 3. Nettoyer les fichiers binaires
dotnet clean
Remove-Item -Recurse -Force ".\EduPlatform.API\bin", ".\EduPlatform.API\obj" -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force ".\EduPlatform.Web\bin", ".\EduPlatform.Web\obj" -ErrorAction SilentlyContinue

# 4. Redémarrer l'infrastructure
docker compose up -d
Start-Sleep -Seconds 60

# 5. Initialiser Cassandra
docker exec -i cassandra_edu cqlsh < .\scripts\cassandra\init.cql

# 6. Créer les topics Kafka
.\scripts\kafka\init-topics.ps1

# 7. Rebuild et lancer l'API
cd .\EduPlatform.API
dotnet restore
dotnet build
dotnet run
```

---

## ?? Support Supplémentaire

Si le problème persiste :

1. Consulter la [documentation complète](docs/)
2. Vérifier les logs détaillés : `docker compose logs -f`
3. Créer un issue sur GitHub avec :
   - Description du problème
   - Messages d'erreur complets
   - Sortie de `docker ps`
   - Sortie de `dotnet --info`
   - Environnement (Windows version, .NET version)

---

**Dernière mise à jour** : 2026-08-28
