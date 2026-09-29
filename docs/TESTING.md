# ?? Guide de Tests - EduPlatform

## Overview

Ce document d�crit la strat�gie de tests pour EduPlatform, incluant les tests unitaires, d'int�gration et end-to-end.

## Smoke E2E public

Le smoke test Playwright couvre le catalogue public, la recherche, les filtres, l'ouverture du d�tail d'un cours, la connexion �tudiant avec acc�s � la progression, les acc�s instructeur et administrateur, la validation d'une cr�ation de cours vide et le cycle persistant cr�ation/archivage/suppression avec s�paration des r�les.

```powershell
Push-Location scripts/e2e
npm ci
npx playwright install chromium
npm test
Pop-Location
```

Le smoke verifie aussi `GET /api/courses/paged` : taille de page, total non pagine, pages adjacentes sans chevauchement et retour borne pour une page hors limites.

`npm test` execute les trois suites. L'audit Axe controle WCAG 2.1 A/AA et fait echouer le job sur les violations `serious` ou `critical` des pages publiques. Pour lancer un controle isole :

```powershell
npm run test:smoke        # parcours public, etudiant, instructeur, admin
npm run test:publication  # cycle publication + moderation
npm run test:a11y         # accueil, catalogue, detail du cours
```

La cible par d�faut est `http://localhost:5297`. Pour tester une autre instance :

```powershell
$env:EDUPLATFORM_BASE_URL = "http://localhost:5297"
$env:EDUPLATFORM_API_URL = "http://localhost:5053"
```

## E2E publication et moderation (donnees persistantes)

`scripts/e2e/publication-moderation.mjs` deroule le cycle de vie complet d'un cours sur des
donnees reellement ecrites en base, puis nettoie tout ce qu'il a cree.

Etapes couvertes :

1. Creation d'un cours en brouillon par l'instructeur (statut `Draft`, non publie)
2. Soumission refusee tant que le cours n'a aucun module (400)
3. Ajout d'un module, puis soumission a la validation (statut `PendingReview`)
4. Presence du cours dans la file `/api/courses/pending` cote admin
5. Rejet sans motif refuse (400), puis rejet motive (statut `Rejected`, motif persiste)
6. Resoumission apres correction, motif de rejet efface
7. Approbation interdite a l'instructeur (403), puis approbation admin (statut `Approved`, publie)
8. Visibilite du cours publie dans le catalogue public anonyme **et** dans l'interface web
9. Inscription gratuite d'un apprenant sans appel a la passerelle de paiement, lecture des questions et soumission d'un QCM avec reponse correcte
10. Persistance du resultat dans Cassandra et lecture par les analytics instructeur
11. Lecture du meilleur resultat depuis Cassandra pour emettre le certificat
12. Depot d'un avis note, auto-signalement refuse (400), signalement par un second inscrit (204), doublon refuse (409)
13. Remontee de l'avis et de son motif dans `/api/admin/reviews`
14. Suppression de l'avis par l'admin (204), disparition cote moderation et cote cours
15. Depublication : le cours sort du catalogue public
16. Nettoyage garanti (bloc `finally`) du cours SQL et des partitions Cassandra `test_results` / `user_activities`

Le test utilise les comptes de demonstration (`instructor@`, `admin@`, `student@`) et exige donc
une stack demarree avec `SEED_DEMO_DATA=true`. Les identifiants crees sont suffixes par un
horodatage, deux executions concurrentes ne se marchent pas dessus.

Les deux suites sont executees par le job `compose-integration` de la CI contre une stack
Docker ephemere.

---

## ?? Strat�gie de Tests

### Pyramide de Tests

```
                    /\
                   /  \
                  / E2E \          ? Peu nombreux, sc�narios critiques
                 /--------\
                /          \
               / Integration \    ? Mod�r�ment nombreux, interactions
              /--------------\
             /                \
            /   Unit Tests     \  ? Tr�s nombreux, logique m�tier
           /____________________\
```

**Objectifs de couverture:**
- Tests unitaires: 80%+ du code
- Tests d'int�gration: Endpoints critiques
- Tests E2E: Parcours utilisateur principaux

---

## ?? Configuration des Projets de Tests

### 1. Cr�er les projets de tests

```bash
# Tests unitaires
dotnet new xunit -n EduPlatform.Tests.Unit -o EduPlatform.Tests.Unit

# Tests d'int�gration
dotnet new xunit -n EduPlatform.Tests.Integration -o EduPlatform.Tests.Integration

# Ajouter � la solution
dotnet sln add EduPlatform.Tests.Unit
dotnet sln add EduPlatform.Tests.Integration
```

### 2. Ajouter les packages NuGet

**Tests Unitaires (EduPlatform.Tests.Unit.csproj):**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.4" />
    <PackageReference Include="coverlet.collector" Version="6.0.0" />
    
    <!-- Mocking -->
    <PackageReference Include="Moq" Version="4.20.70" />
    
    <!-- Assertions fluides -->
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
    
    <!-- Fake data -->
    <PackageReference Include="Bogus" Version="35.0.1" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\EduPlatform.API\EduPlatform.API.csproj" />
    <ProjectReference Include="..\EduPlatform.Core\EduPlatform.Core.csproj" />
    <ProjectReference Include="..\EduPlatform.Data\EduPlatform.Data.csproj" />
  </ItemGroup>
</Project>
```

**Tests d'Int�gration (EduPlatform.Tests.Integration.csproj):**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.4" />
    
    <!-- Testing API -->
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.0" />
    
    <!-- In-memory database -->
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.0" />
    
    <!-- Test Containers -->
    <PackageReference Include="Testcontainers" Version="3.6.0" />
    <PackageReference Include="Testcontainers.MsSql" Version="3.6.0" />
    
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\EduPlatform.API\EduPlatform.API.csproj" />
  </ItemGroup>
</Project>
```

---

## ?? Tests Unitaires

### Structure du projet

```
EduPlatform.Tests.Unit/
??? Controllers/
?   ??? AuthControllerTests.cs
?   ??? CoursesControllerTests.cs
?   ??? TestsControllerTests.cs
??? Services/
?   ??? AuthServiceTests.cs
?   ??? ChatbotServiceTests.cs
?   ??? AnalyticsServiceTests.cs
??? Repositories/
?   ??? ActivityRepositoryTests.cs
?   ??? CacheServiceTests.cs
??? Helpers/
?   ??? TestDataBuilder.cs
??? Fixtures/
    ??? DatabaseFixture.cs
```

### Exemples de tests unitaires

#### **AuthServiceTests.cs**

```csharp
using EduPlatform.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace EduPlatform.Tests.Unit.Services
{
    public class AuthServiceTests
    {
        private readonly AuthService _authService;

        public AuthServiceTests()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    {"Jwt:Key", "SuperSecretKeyForTestingPurposes12345!"},
                    {"Jwt:Issuer", "EduPlatform"},
                    {"Jwt:Audience", "EduPlatformUsers"},
                    {"Jwt:ExpirationHours", "24"}
                })
                .Build();

            _authService = new AuthService(configuration);
        }

        [Fact]
        public void HashPassword_ShouldReturnDifferentHashForSamePassword()
        {
            // Arrange
            var password = "TestPassword123!";

            // Act
            var hash1 = _authService.HashPassword(password);
            var hash2 = _authService.HashPassword(password);

            // Assert
            hash1.Should().NotBe(hash2);
            hash1.Should().NotBeNullOrEmpty();
            hash2.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void VerifyPassword_ShouldReturnTrue_WhenPasswordIsCorrect()
        {
            // Arrange
            var password = "TestPassword123!";
            var hash = _authService.HashPassword(password);

            // Act
            var result = _authService.VerifyPassword(password, hash);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void VerifyPassword_ShouldReturnFalse_WhenPasswordIsIncorrect()
        {
            // Arrange
            var correctPassword = "TestPassword123!";
            var wrongPassword = "WrongPassword456!";
            var hash = _authService.HashPassword(correctPassword);

            // Act
            var result = _authService.VerifyPassword(wrongPassword, hash);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void GenerateToken_ShouldReturnValidJwt()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                FirstName = "Test",
                LastName = "User",
                Role = UserRole.Student
            };

            // Act
            var token = _authService.GenerateToken(user);

            // Assert
            token.Should().NotBeNullOrEmpty();
            token.Should().Contain(".");
            token.Split('.').Should().HaveCount(3); // Header.Payload.Signature
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void HashPassword_ShouldThrowException_WhenPasswordIsNullOrEmpty(string password)
        {
            // Act
            Action act = () => _authService.HashPassword(password);

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }
}
```

#### **CoursesControllerTests.cs**

```csharp
using EduPlatform.API.Controllers;
using EduPlatform.Core.Models;
using EduPlatform.Data.Cache;
using EduPlatform.Data.SqlServer;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace EduPlatform.Tests.Unit.Controllers
{
    public class CoursesControllerTests
    {
        private readonly Mock<CacheService> _cacheServiceMock;
        private readonly EduDbContext _dbContext;
        private readonly CoursesController _controller;

        public CoursesControllerTests()
        {
            // Setup in-memory database
            var options = new DbContextOptionsBuilder<EduDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _dbContext = new EduDbContext(options);
            _cacheServiceMock = new Mock<CacheService>(null);
            _controller = new CoursesController(_dbContext, _cacheServiceMock.Object);
        }

        [Fact]
        public async Task GetAll_ShouldReturnAllPublishedCourses()
        {
            // Arrange
            var courses = new List<Course>
            {
                new Course { Id = Guid.NewGuid(), Title = "Course 1", IsPublished = true },
                new Course { Id = Guid.NewGuid(), Title = "Course 2", IsPublished = true },
                new Course { Id = Guid.NewGuid(), Title = "Course 3", IsPublished = false }
            };

            _dbContext.Courses.AddRange(courses);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _controller.GetAll(null, null);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedCourses = okResult.Value.Should().BeAssignableTo<List<Course>>().Subject;
            returnedCourses.Should().HaveCount(2);
            returnedCourses.Should().AllSatisfy(c => c.IsPublished.Should().BeTrue());
        }

        [Fact]
        public async Task GetAll_ShouldFilterByCategory()
        {
            // Arrange
            var courses = new List<Course>
            {
                new Course { Title = "BigData Course", Category = "BigData", IsPublished = true },
                new Course { Title = "IA Course", Category = "IA", IsPublished = true }
            };

            _dbContext.Courses.AddRange(courses);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _controller.GetAll("BigData", null);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedCourses = okResult.Value.Should().BeAssignableTo<List<Course>>().Subject;
            returnedCourses.Should().HaveCount(1);
            returnedCourses.First().Category.Should().Be("BigData");
        }

        [Fact]
        public async Task GetById_ShouldReturnCourse_WhenExists()
        {
            // Arrange
            var courseId = Guid.NewGuid();
            var course = new Course
            {
                Id = courseId,
                Title = "Test Course",
                IsPublished = true
            };

            _dbContext.Courses.Add(course);
            await _dbContext.SaveChangesAsync();

            _cacheServiceMock
                .Setup(x => x.GetAsync<Course>(It.IsAny<string>()))
                .ReturnsAsync((Course)null);

            // Act
            var result = await _controller.GetById(courseId);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var returnedCourse = okResult.Value.Should().BeOfType<Course>().Subject;
            returnedCourse.Id.Should().Be(courseId);
        }

        [Fact]
        public async Task GetById_ShouldReturnNotFound_WhenCourseDoesNotExist()
        {
            // Arrange
            var nonExistentId = Guid.NewGuid();

            _cacheServiceMock
                .Setup(x => x.GetAsync<Course>(It.IsAny<string>()))
                .ReturnsAsync((Course)null);

            // Act
            var result = await _controller.GetById(nonExistentId);

            // Assert
            result.Should().BeOfType<NotFoundResult>();
        }

        public void Dispose()
        {
            _dbContext.Dispose();
        }
    }
}
```

---

## ?? Tests d'Int�gration

### Structure du projet

```
EduPlatform.Tests.Integration/
??? Controllers/
?   ??? AuthControllerIntegrationTests.cs
?   ??? CoursesControllerIntegrationTests.cs
??? Infrastructure/
?   ??? WebApplicationFactoryFixture.cs
?   ??? DatabaseSeeder.cs
??? Helpers/
    ??? HttpClientExtensions.cs
```

### Exemple de tests d'int�gration

#### **WebApplicationFactoryFixture.cs**

```csharp
using EduPlatform.Data.SqlServer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EduPlatform.Tests.Integration.Infrastructure
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Remove the real database
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<EduDbContext>));

                if (descriptor != null)
                    services.Remove(descriptor);

                // Add in-memory database
                services.AddDbContext<EduDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDatabase");
                });

                // Build the service provider
                var sp = services.BuildServiceProvider();

                // Create a scope to get the database
                using var scope = sp.CreateScope();
                var scopedServices = scope.ServiceProvider;
                var db = scopedServices.GetRequiredService<EduDbContext>();

                // Ensure database is created
                db.Database.EnsureCreated();

                // Seed test data
                DatabaseSeeder.Seed(db);
            });
        }
    }
}
```

#### **AuthControllerIntegrationTests.cs**

```csharp
using System.Net;
using System.Net.Http.Json;
using EduPlatform.Tests.Integration.Infrastructure;
using FluentAssertions;
using Xunit;

namespace EduPlatform.Tests.Integration.Controllers
{
    public class AuthControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client;

        public AuthControllerIntegrationTests(CustomWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Register_ShouldReturnOk_WithValidData()
        {
            // Arrange
            var registerDto = new
            {
                email = "newuser@example.com",
                password = "SecurePassword123!",
                firstName = "John",
                lastName = "Doe"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/register", registerDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var result = await response.Content.ReadFromJsonAsync<dynamic>();
            result.Should().NotBeNull();
            result.token.Should().NotBeNullOrEmpty();
            result.userId.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Register_ShouldReturnBadRequest_WhenEmailAlreadyExists()
        {
            // Arrange
            var registerDto = new
            {
                email = "existing@example.com",
                password = "SecurePassword123!",
                firstName = "John",
                lastName = "Doe"
            };

            // Register first time
            await _client.PostAsJsonAsync("/api/auth/register", registerDto);

            // Act - Try to register again with same email
            var response = await _client.PostAsJsonAsync("/api/auth/register", registerDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Login_ShouldReturnOk_WithCorrectCredentials()
        {
            // Arrange
            var email = "logintest@example.com";
            var password = "SecurePassword123!";

            // Register user first
            await _client.PostAsJsonAsync("/api/auth/register", new
            {
                email,
                password,
                firstName = "Test",
                lastName = "User"
            });

            var loginDto = new { email, password };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/login", loginDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var result = await response.Content.ReadFromJsonAsync<dynamic>();
            result.token.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Login_ShouldReturnUnauthorized_WithWrongPassword()
        {
            // Arrange
            var loginDto = new
            {
                email = "test@example.com",
                password = "WrongPassword123!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/login", loginDto);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}
```

---

## ?? Tests End-to-End (E2E)

### Utiliser Playwright

```bash
# Installer Playwright
dotnet new nunit -n EduPlatform.Tests.E2E
cd EduPlatform.Tests.E2E
dotnet add package Microsoft.Playwright.NUnit
pwsh bin/Debug/net8.0/playwright.ps1 install
```

### Exemple de test E2E

```csharp
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace EduPlatform.Tests.E2E
{
    [TestFixture]
    public class UserJourneyTests : PageTest
    {
        [Test]
        public async Task UserCanRegisterLoginAndEnrollInCourse()
        {
            // Navigate to app
            await Page.GotoAsync("https://localhost:7286");

            // Click Register
            await Page.ClickAsync("text=S'inscrire");

            // Fill registration form
            await Page.FillAsync("input[type=email]", "e2etest@example.com");
            await Page.FillAsync("input[type=password]", "SecurePassword123!");
            await Page.FillAsync("input[placeholder*='Pr�nom']", "E2E");
            await Page.FillAsync("input[placeholder*='Nom']", "Test");

            // Submit
            await Page.ClickAsync("button:has-text('Cr�er un compte')");

            // Should be redirected to courses
            await Expect(Page).ToHaveURLAsync(new Regex(".*courses.*"));

            // Find a course and enroll
            await Page.ClickAsync(".course-card:first-child");

            // Click enroll button
            await Page.ClickAsync("button:has-text('S\\'inscrire')");

            // Should see success message
            await Expect(Page.Locator("text=Inscription r�ussie")).ToBeVisibleAsync();
        }
    }
}
```

---

## Couverture de Code

La couverture est collectee par Coverlet et pilotee par `EduPlatform.Tests/coverlet.runsettings`,
qui restreint la mesure au code metier (API, Core, Data, BigData) et exclut les migrations EF,
les DTO et le code genere.

### Executer les tests avec couverture

```bash
dotnet test EduPlatform.Tests/EduPlatform.Tests.csproj --settings EduPlatform.Tests/coverlet.runsettings --collect:"XPlat Code Coverage" --results-directory TestResults
```

Les rapports sont ecrits dans `TestResults/<guid>/coverage.cobertura.xml` et `coverage.opencover.xml`.

### Generer le rapport HTML

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool

reportgenerator -reports:TestResults/**/coverage.cobertura.xml -targetdir:TestResults/coverage-report -reporttypes:"Html;TextSummary"

start TestResults/coverage-report/index.html
```

### Seuil applique en CI

Le job `build-test` echoue si la couverture de lignes passe sous `COVERAGE_THRESHOLD`
(defini en tete de `.github/workflows/ci.yml`). Il s agit d un **seuil-cliquet** : il ne doit
jamais baisser. Quand la couverture progresse durablement, relever la valeur du meme montant
dans le workflow, en un commit dedie.

| Date | Lignes | Branches | Seuil CI |
|------|--------|----------|----------|
| 2026-09-29 | 47,1 % | 25,0 % | 45 % |

Couverture par assembly au 2026-09-29 (build Release, celui mesure en CI) :
Core 96,1 %, Data 66,0 %, API 7,5 %, BigData 0 %.
L objectif roadmap reste **80 %** ; l ecart principal porte sur les controllers de
`EduPlatform.API` et sur `EduPlatform.BigData`.

---

## ?? Ex�cution des Tests

### Commandes utiles

```bash
# Tous les tests
dotnet test

# Tests unitaires uniquement
dotnet test --filter Category=Unit

# Tests d'int�gration uniquement
dotnet test --filter Category=Integration

# Ex�cuter avec logs d�taill�s
dotnet test --logger "console;verbosity=detailed"

# Tests en parall�le
dotnet test --parallel

# Un test sp�cifique
dotnet test --filter "FullyQualifiedName~AuthServiceTests.HashPassword"
```

### CI/CD avec GitHub Actions

Cr�er `.github/workflows/tests.yml`:

```yaml
name: Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: 8.0.x
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore
    
    - name: Run Unit Tests
      run: dotnet test EduPlatform.Tests.Unit --no-build --verbosity normal
    
    - name: Run Integration Tests
      run: dotnet test EduPlatform.Tests.Integration --no-build --verbosity normal
    
    - name: Generate Coverage Report
      run: |
        dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
        dotnet tool install -g dotnet-reportgenerator-globaltool
        reportgenerator -reports:**/coverage.opencover.xml -targetdir:coverage -reporttypes:Html
    
    - name: Upload Coverage
      uses: actions/upload-artifact@v3
      with:
        name: coverage-report
        path: coverage/
```

---

## ?? Best Practices

### 1. Nommage des tests
```csharp
// Pattern: MethodName_Scenario_ExpectedResult
[Fact]
public void HashPassword_WithValidPassword_ShouldReturnHash() { }

[Fact]
public void GetById_WhenCourseNotFound_ShouldReturnNotFound() { }
```

### 2. Arrange-Act-Assert (AAA)
```csharp
[Fact]
public void Example()
{
    // Arrange - Setup
    var service = new MyService();
    
    // Act - Execute
    var result = service.DoSomething();
    
    // Assert - Verify
    result.Should().BeTrue();
}
```

### 3. Isolation des tests
- Chaque test doit �tre ind�pendant
- Utiliser des bases de donn�es distinctes
- Nettoyer apr�s chaque test

### 4. Tests param�tr�s
```csharp
[Theory]
[InlineData("", false)]
[InlineData("   ", false)]
[InlineData("valid@email.com", true)]
public void ValidateEmail_ShouldReturnExpectedResult(string email, bool expected)
{
    var result = _validator.IsValidEmail(email);
    result.Should().Be(expected);
}
```

---

## ?? Checklist Tests

- [ ] Tests unitaires pour tous les services
- [ ] Tests unitaires pour tous les controllers
- [ ] Tests unitaires pour tous les repositories
- [ ] Tests d'int�gration pour les endpoints API
- [ ] Tests E2E pour les parcours utilisateur critiques
- [ ] Couverture de code > 80%
- [ ] Tests dans la CI/CD pipeline
- [ ] Documentation des sc�narios de test

**Derni�re mise � jour:** 2026-08-24
