# ?? Guide de Déploiement - EduPlatform

## Vue d'ensemble

Ce guide couvre le déploiement de EduPlatform depuis le développement local jusqu'à la production sur Azure/AWS.

---

## ??? Environnements

| Environnement | Description | URL |
|---------------|-------------|-----|
| **Development** | Machine locale des développeurs | localhost |
| **Staging** | Environnement de pré-production | staging.eduplatform.com |
| **Production** | Environnement de production | eduplatform.com |

---

## ?? Prérequis

### Développement Local

- .NET 8 SDK
- SQL Server Express/Developer
- Docker Desktop (pour Cassandra, Kafka, Redis)
- Visual Studio 2022 / VS Code
- Git

### Production

- Compte Azure / AWS
- Azure CLI / AWS CLI
- Docker (pour containerisation)
- kubectl (pour Kubernetes, optionnel)

---

## ?? Configuration par Environnement

### appsettings.Development.json

```json
{
  "ConnectionStrings": {
    "SqlServer": "Server=localhost\\SQLEXPRESS;Database=EduPlatformDB;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "Cassandra": {
    "Host": "localhost",
    "Port": "9042",
    "Keyspace": "edu_platform"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

### appsettings.Staging.json

```json
{
  "ConnectionStrings": {
    "SqlServer": "Server=eduplatform-staging.database.windows.net;Database=EduPlatformDB;User Id=admin;Password=***;"
  },
  "Cassandra": {
    "Host": "cassandra-staging.eastus.cloudapp.azure.com",
    "Port": "9042",
    "Keyspace": "edu_platform",
    "Username": "cassandra",
    "Password": "***"
  },
  "Redis": {
    "ConnectionString": "eduplatform-staging.redis.cache.windows.net:6380,password=***,ssl=True"
  },
  "Kafka": {
    "BootstrapServers": "eduplatform-staging-kafka.servicebus.windows.net:9093",
    "SecurityProtocol": "SASL_SSL"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### appsettings.Production.json

```json
{
  "ConnectionStrings": {
    "SqlServer": "*** (Azure Key Vault) ***"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "eduplatform.com,www.eduplatform.com"
}
```

---

## ?? Gestion des Secrets

### Development: User Secrets

```bash
cd EduPlatform.API

# Initialiser User Secrets
dotnet user-secrets init

# Ajouter les secrets
dotnet user-secrets set "OpenAI:ApiKey" "sk-your-openai-key"
dotnet user-secrets set "Jwt:Key" "your-super-secret-jwt-key-min-32-characters!"
dotnet user-secrets set "ConnectionStrings:SqlServer" "Server=..."

# Lister les secrets
dotnet user-secrets list

# Supprimer un secret
dotnet user-secrets remove "OpenAI:ApiKey"

# Effacer tous les secrets
dotnet user-secrets clear
```

### Production: Azure Key Vault

**1. Créer un Key Vault:**

```bash
# Azure CLI
az login

az keyvault create \
  --name eduplatform-keyvault \
  --resource-group eduplatform-rg \
  --location eastus
```

**2. Ajouter des secrets:**

```bash
az keyvault secret set \
  --vault-name eduplatform-keyvault \
  --name "ConnectionStrings--SqlServer" \
  --value "Server=..."

az keyvault secret set \
  --vault-name eduplatform-keyvault \
  --name "OpenAI--ApiKey" \
  --value "sk-..."

az keyvault secret set \
  --vault-name eduplatform-keyvault \
  --name "Jwt--Key" \
  --value "your-jwt-key"
```

**3. Configurer l'accès dans Program.cs:**

```csharp
// Program.cs
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

var builder = WebApplication.CreateBuilder(args);

// En production, charger depuis Key Vault
if (builder.Environment.IsProduction())
{
    var keyVaultUrl = builder.Configuration["KeyVault:Url"];
    var secretClient = new SecretClient(
        new Uri(keyVaultUrl), 
        new DefaultAzureCredential()
    );
    
    builder.Configuration.AddAzureKeyVault(
        secretClient, 
        new KeyVaultSecretManager()
    );
}
```

---

## ?? Containerisation

### Dockerfile pour l'API

Créer `EduPlatform.API/Dockerfile`:

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and projects
COPY ["EduPlatform.sln", "./"]
COPY ["EduPlatform.API/EduPlatform.API.csproj", "EduPlatform.API/"]
COPY ["EduPlatform.Core/EduPlatform.Core.csproj", "EduPlatform.Core/"]
COPY ["EduPlatform.Data/EduPlatform.Data.csproj", "EduPlatform.Data/"]
COPY ["EduPlatform.BigData/EduPlatform.BigData.csproj", "EduPlatform.BigData/"]

# Restore dependencies
RUN dotnet restore "EduPlatform.API/EduPlatform.API.csproj"

# Copy all source code
COPY . .

# Build
WORKDIR "/src/EduPlatform.API"
RUN dotnet build "EduPlatform.API.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "EduPlatform.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EduPlatform.API.dll"]
```

### Dockerfile pour le Web

Créer `EduPlatform.Web/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["EduPlatform.Web/EduPlatform.Web.csproj", "EduPlatform.Web/"]
RUN dotnet restore "EduPlatform.Web/EduPlatform.Web.csproj"

COPY . .
WORKDIR "/src/EduPlatform.Web"
RUN dotnet build "EduPlatform.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "EduPlatform.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EduPlatform.Web.dll"]
```

### Build et test des images

```bash
# Build API
docker build -t eduplatform-api:latest -f EduPlatform.API/Dockerfile .

# Build Web
docker build -t eduplatform-web:latest -f EduPlatform.Web/Dockerfile .

# Test local
docker run -d -p 8080:80 --name eduplatform-api eduplatform-api:latest
docker run -d -p 8081:80 --name eduplatform-web eduplatform-web:latest

# Voir les logs
docker logs eduplatform-api
docker logs eduplatform-web

# Arrêter et supprimer
docker stop eduplatform-api eduplatform-web
docker rm eduplatform-api eduplatform-web
```

### docker-compose pour production

Créer `docker-compose.prod.yml`:

```yaml
version: '3.8'

services:
  api:
    image: eduplatform-api:latest
    ports:
      - "80:80"
      - "443:443"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=https://+:443;http://+:80
    volumes:
      - ./certs:/https:ro
    depends_on:
      - cassandra
      - kafka
      - redis

  web:
    image: eduplatform-web:latest
    ports:
      - "8080:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    depends_on:
      - api

  cassandra:
    image: cassandra:4.1
    ports:
      - "9042:9042"
    volumes:
      - cassandra_data:/var/lib/cassandra
    environment:
      - CASSANDRA_CLUSTER_NAME=EduCluster

  kafka:
    image: confluentinc/cp-kafka:7.5.0
    ports:
      - "9092:9092"
    environment:
      - KAFKA_BROKER_ID=1
      - KAFKA_ZOOKEEPER_CONNECT=zookeeper:2181
      - KAFKA_ADVERTISED_LISTENERS=PLAINTEXT://kafka:9092
    depends_on:
      - zookeeper

  zookeeper:
    image: confluentinc/cp-zookeeper:7.5.0
    ports:
      - "2181:2181"
    environment:
      - ZOOKEEPER_CLIENT_PORT=2181

  redis:
    image: redis:7.2
    ports:
      - "6379:6379"
    volumes:
      - redis_data:/data

volumes:
  cassandra_data:
  redis_data:
```

---

## ?? Déploiement sur Azure

### 1. Créer les ressources Azure

```bash
# Variables
RESOURCE_GROUP="eduplatform-rg"
LOCATION="eastus"
APP_SERVICE_PLAN="eduplatform-plan"
WEB_APP_API="eduplatform-api"
WEB_APP_WEB="eduplatform-web"

# Créer le groupe de ressources
az group create --name $RESOURCE_GROUP --location $LOCATION

# Créer le plan App Service
az appservice plan create \
  --name $APP_SERVICE_PLAN \
  --resource-group $RESOURCE_GROUP \
  --sku B1 \
  --is-linux

# Créer l'App Service pour l'API
az webapp create \
  --name $WEB_APP_API \
  --resource-group $RESOURCE_GROUP \
  --plan $APP_SERVICE_PLAN \
  --runtime "DOTNETCORE:8.0"

# Créer l'App Service pour le Web
az webapp create \
  --name $WEB_APP_WEB \
  --resource-group $RESOURCE_GROUP \
  --plan $APP_SERVICE_PLAN \
  --runtime "DOTNETCORE:8.0"
```

### 2. Créer Azure SQL Database

```bash
SQL_SERVER="eduplatform-sqlserver"
SQL_DB="EduPlatformDB"
SQL_ADMIN="sqladmin"
SQL_PASSWORD="YourSecurePassword123!"

# Créer le serveur SQL
az sql server create \
  --name $SQL_SERVER \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --admin-user $SQL_ADMIN \
  --admin-password $SQL_PASSWORD

# Créer la base de données
az sql db create \
  --name $SQL_DB \
  --server $SQL_SERVER \
  --resource-group $RESOURCE_GROUP \
  --service-objective S0

# Configurer le firewall (autoriser Azure services)
az sql server firewall-rule create \
  --server $SQL_SERVER \
  --resource-group $RESOURCE_GROUP \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0
```

### 3. Créer Azure Cache for Redis

```bash
REDIS_NAME="eduplatform-redis"

az redis create \
  --name $REDIS_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku Basic \
  --vm-size c0
```

### 4. Déployer l'application

**Option A: Déploiement depuis le code**

```bash
# API
cd EduPlatform.API
az webapp up \
  --name $WEB_APP_API \
  --resource-group $RESOURCE_GROUP

# Web
cd ../EduPlatform.Web
az webapp up \
  --name $WEB_APP_WEB \
  --resource-group $RESOURCE_GROUP
```

**Option B: Déploiement depuis Docker**

```bash
# Créer Azure Container Registry
ACR_NAME="eduplatformacr"

az acr create \
  --name $ACR_NAME \
  --resource-group $RESOURCE_GROUP \
  --sku Basic \
  --admin-enabled true

# Login
az acr login --name $ACR_NAME

# Tag et push les images
docker tag eduplatform-api:latest $ACR_NAME.azurecr.io/eduplatform-api:latest
docker tag eduplatform-web:latest $ACR_NAME.azurecr.io/eduplatform-web:latest

docker push $ACR_NAME.azurecr.io/eduplatform-api:latest
docker push $ACR_NAME.azurecr.io/eduplatform-web:latest

# Configurer Web App pour utiliser l'image
az webapp config container set \
  --name $WEB_APP_API \
  --resource-group $RESOURCE_GROUP \
  --docker-custom-image-name $ACR_NAME.azurecr.io/eduplatform-api:latest \
  --docker-registry-server-url https://$ACR_NAME.azurecr.io
```

### 5. Configurer les variables d'environnement

```bash
# Récupérer la connection string SQL
SQL_CONNECTION=$(az sql db show-connection-string \
  --client ado.net \
  --server $SQL_SERVER \
  --name $SQL_DB)

# Configurer les App Settings
az webapp config appsettings set \
  --name $WEB_APP_API \
  --resource-group $RESOURCE_GROUP \
  --settings \
    ASPNETCORE_ENVIRONMENT="Production" \
    ConnectionStrings__SqlServer="$SQL_CONNECTION"
```

### 6. Exécuter les migrations

```bash
# Depuis votre machine locale (connecté à Azure SQL)
dotnet ef database update \
  --project EduPlatform.Data \
  --startup-project EduPlatform.API \
  --connection "Server=..."
```

---

## ?? CI/CD avec GitHub Actions

Créer `.github/workflows/deploy.yml`:

```yaml
name: Deploy to Azure

on:
  push:
    branches: [ main ]

env:
  AZURE_WEBAPP_API: eduplatform-api
  AZURE_WEBAPP_WEB: eduplatform-web
  DOTNET_VERSION: '8.0.x'

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --configuration Release --no-restore
    
    - name: Test
      run: dotnet test --no-build --verbosity normal
    
    - name: Publish API
      run: dotnet publish EduPlatform.API/EduPlatform.API.csproj -c Release -o ./publish-api
    
    - name: Publish Web
      run: dotnet publish EduPlatform.Web/EduPlatform.Web.csproj -c Release -o ./publish-web
    
    - name: Deploy API to Azure
      uses: azure/webapps-deploy@v2
      with:
        app-name: ${{ env.AZURE_WEBAPP_API }}
        publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE_API }}
        package: ./publish-api
    
    - name: Deploy Web to Azure
      uses: azure/webapps-deploy@v2
      with:
        app-name: ${{ env.AZURE_WEBAPP_WEB }}
        publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE_WEB }}
        package: ./publish-web
```

---

## ?? Monitoring et Logging

### Application Insights

```bash
# Créer Application Insights
APPINSIGHTS_NAME="eduplatform-insights"

az monitor app-insights component create \
  --app $APPINSIGHTS_NAME \
  --location $LOCATION \
  --resource-group $RESOURCE_GROUP \
  --application-type web

# Récupérer la clé d'instrumentation
INSTRUMENTATION_KEY=$(az monitor app-insights component show \
  --app $APPINSIGHTS_NAME \
  --resource-group $RESOURCE_GROUP \
  --query instrumentationKey -o tsv)

# Configurer dans l'App Service
az webapp config appsettings set \
  --name $WEB_APP_API \
  --resource-group $RESOURCE_GROUP \
  --settings ApplicationInsights__InstrumentationKey="$INSTRUMENTATION_KEY"
```

**Dans le code (Program.cs):**

```csharp
builder.Services.AddApplicationInsightsTelemetry(
    builder.Configuration["ApplicationInsights:InstrumentationKey"]
);
```

---

## ?? Sécurité en Production

### 1. HTTPS obligatoire

```csharp
// Program.cs
app.UseHttpsRedirection();
app.UseHsts();
```

### 2. Rate Limiting

```bash
dotnet add package AspNetCoreRateLimit
```

```csharp
// Program.cs
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

app.UseIpRateLimiting();
```

### 3. Configurer CORS en production

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins("https://eduplatform.com", "https://www.eduplatform.com")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
```

---

## ? Checklist de Déploiement

### Pre-Deployment
- [ ] Tests passent en local
- [ ] Tests passent dans CI/CD
- [ ] Variables d'environnement configurées
- [ ] Secrets stockés dans Key Vault
- [ ] Base de données créée
- [ ] Migrations testées
- [ ] Certificats SSL configurés

### Deployment
- [ ] Build réussi
- [ ] Images Docker créées
- [ ] Déploiement sur Azure/AWS
- [ ] Health checks OK
- [ ] Logs consultables
- [ ] Monitoring actif

### Post-Deployment
- [ ] Tests smoke en production
- [ ] Vérification des endpoints
- [ ] Vérification des logs
- [ ] Alertes configurées
- [ ] Documentation à jour

---

## ?? Rollback

### Azure App Service

```bash
# Lister les déploiements
az webapp deployment list \
  --name $WEB_APP_API \
  --resource-group $RESOURCE_GROUP

# Rollback vers un déploiement précédent
az webapp deployment slot swap \
  --name $WEB_APP_API \
  --resource-group $RESOURCE_GROUP \
  --slot staging \
  --target-slot production
```

### Docker

```bash
# Déployer une version précédente
docker pull eduplatformacr.azurecr.io/eduplatform-api:previous-tag
docker run -d eduplatformacr.azurecr.io/eduplatform-api:previous-tag
```

---

## ?? Ressources

- [Azure App Service Docs](https://docs.microsoft.com/en-us/azure/app-service/)
- [Docker Best Practices](https://docs.docker.com/develop/dev-best-practices/)
- [GitHub Actions for Azure](https://github.com/Azure/actions)

**Dernière mise à jour:** 2026-08-24
