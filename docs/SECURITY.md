# ?? Guide de Sécurité - EduPlatform

## Vue d'ensemble

Ce document décrit les pratiques de sécurité implémentées et recommandées pour EduPlatform.

---

## ?? Authentification & Autorisation

### JWT (JSON Web Tokens)

**Implémentation actuelle:**

```csharp
// AuthService.cs - Génération de token
public string GenerateToken(User user)
{
    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Role, user.Role.ToString())
    };

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: _config["Jwt:Issuer"],
        audience: _config["Jwt:Audience"],
        claims: claims,
        expires: DateTime.UtcNow.AddHours(24),
        signingCredentials: creds
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

**Bonnes pratiques:**

- ? Utiliser HMAC-SHA256 minimum
- ? Expiration à 24h maximum
- ? Clé JWT >= 32 caractères
- ?? **À FAIRE:** Implémenter refresh tokens
- ?? **À FAIRE:** Blacklist de tokens révoqués

### Gestion des Mots de Passe

**Hashing avec BCrypt:**

```csharp
public string HashPassword(string password)
{
    return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
}

public bool VerifyPassword(string password, string hash)
{
    return BCrypt.Net.BCrypt.Verify(password, hash);
}
```

**Politique de mots de passe recommandée:**

- Minimum 8 caractères
- Au moins 1 majuscule
- Au moins 1 minuscule
- Au moins 1 chiffre
- Au moins 1 caractère spécial

**Validation côté API:**

```csharp
using System.ComponentModel.DataAnnotations;

public class PasswordValidator : ValidationAttribute
{
    protected override ValidationResult IsValid(object value, ValidationContext context)
    {
        var password = value as string;
        
        if (string.IsNullOrEmpty(password) || password.Length < 8)
            return new ValidationResult("Le mot de passe doit contenir au moins 8 caractères");
        
        if (!password.Any(char.IsUpper))
            return new ValidationResult("Le mot de passe doit contenir au moins une majuscule");
        
        if (!password.Any(char.IsLower))
            return new ValidationResult("Le mot de passe doit contenir au moins une minuscule");
        
        if (!password.Any(char.IsDigit))
            return new ValidationResult("Le mot de passe doit contenir au moins un chiffre");
        
        return ValidationResult.Success;
    }
}
```

### Autorisation basée sur les rôles

```csharp
// Différents niveaux d'accès
[Authorize(Roles = "Student")]           // Étudiants uniquement
[Authorize(Roles = "Instructor,Admin")]  // Instructeurs ou Admins
[Authorize]                              // Tout utilisateur authentifié
```

**Matrice d'autorisation:**

| Action | Student | Instructor | Admin |
|--------|---------|------------|-------|
| Voir les cours | ? | ? | ? |
| S'inscrire à un cours | ? | ? | ? |
| Créer un cours | ? | ? | ? |
| Modifier un cours | ? | ? (sien) | ? |
| Supprimer un cours | ? | ? | ? |
| Gérer les utilisateurs | ? | ? | ? |

---

## ??? Protection contre les Attaques

### 1. SQL Injection

**? Protection via Entity Framework Core:**

EF Core utilise des requêtes paramétrées par défaut:

```csharp
// SÉCURISÉ - Requête paramétrée
var user = await _db.Users
    .FirstOrDefaultAsync(u => u.Email == email);

// DANGEREUX - À NE JAMAIS FAIRE
var user = await _db.Users
    .FromSqlRaw($"SELECT * FROM Users WHERE Email = '{email}'")
    .FirstOrDefaultAsync();
```

**Pour les requêtes SQL brutes, utiliser des paramètres:**

```csharp
var email = "user@example.com";
var user = await _db.Users
    .FromSqlInterpolated($"SELECT * FROM Users WHERE Email = {email}")
    .FirstOrDefaultAsync();
```

### 2. XSS (Cross-Site Scripting)

**? Protection automatique de Blazor:**

Blazor encode automatiquement les valeurs:

```razor
@* Encodé automatiquement - SÉCURISÉ *@
<p>@userInput</p>

@* HTML non encodé - DANGEREUX *@
<p>@((MarkupString)userInput)</p>
```

**Validation et sanitization:**

```csharp
using System.Web;

public string SanitizeInput(string input)
{
    return HttpUtility.HtmlEncode(input);
}
```

### 3. CSRF (Cross-Site Request Forgery)

**Configuration dans Program.cs:**

```csharp
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

app.UseAntiforgery();
```

**Dans les composants Blazor:**

```razor
@inject Microsoft.AspNetCore.Antiforgery.IAntiforgery Antiforgery

<form method="post">
    @Antiforgery.GetHtml()
    <!-- formulaire -->
</form>
```

### 4. Rate Limiting

**Installation:**

```bash
dotnet add package AspNetCoreRateLimit
```

**Configuration (Program.cs):**

```csharp
// Configuration
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.GeneralRules = new List<RateLimitRule>
    {
        new RateLimitRule
        {
            Endpoint = "*",
            Limit = 100,
            Period = "1m"
        },
        new RateLimitRule
        {
            Endpoint = "*/auth/login",
            Limit = 5,
            Period = "5m"
        },
        new RateLimitRule
        {
            Endpoint = "*/chatbot/message",
            Limit = 20,
            Period = "1m"
        }
    };
});

builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// Middleware
app.UseIpRateLimiting();
```

**appsettings.json:**

```json
{
  "IpRateLimiting": {
    "EnableEndpointRateLimiting": true,
    "StackBlockedRequests": false,
    "RealIpHeader": "X-Real-IP",
    "HttpStatusCode": 429
  }
}
```

### 5. Attaques par force brute

**Stratégies:**

1. **Rate limiting sur /login** (5 tentatives / 5 min)
2. **Compte verrouillé après X tentatives**
3. **CAPTCHA après 3 tentatives échouées**

**Implémentation:**

```csharp
public class LoginAttemptService
{
    private readonly IMemoryCache _cache;
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<bool> IsLockedOut(string email)
    {
        var key = $"login_attempts_{email}";
        if (_cache.TryGetValue(key, out int attempts))
        {
            return attempts >= MaxAttempts;
        }
        return false;
    }

    public async Task RecordFailedAttempt(string email)
    {
        var key = $"login_attempts_{email}";
        var attempts = _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = LockoutDuration;
            return 0;
        });
        
        _cache.Set(key, attempts + 1, LockoutDuration);
    }

    public async Task ResetAttempts(string email)
    {
        var key = $"login_attempts_{email}";
        _cache.Remove(key);
    }
}
```

---

## ?? Sécurité des Données

### 1. Chiffrement en transit (HTTPS)

**Configuration HTTPS obligatoire:**

```csharp
// Program.cs
app.UseHttpsRedirection();

if (app.Environment.IsProduction())
{
    app.UseHsts();
}
```

**Forcer HTTPS dans launchSettings.json:**

```json
{
  "profiles": {
    "https": {
      "commandName": "Project",
      "launchBrowser": true,
      "applicationUrl": "https://localhost:7194",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

### 2. Chiffrement au repos

**Données sensibles dans la base:**

```csharp
using System.Security.Cryptography;
using System.Text;

public class EncryptionService
{
    private readonly byte[] _key;
    private readonly byte[] _iv;

    public EncryptionService(IConfiguration config)
    {
        _key = Encoding.UTF8.GetBytes(config["Encryption:Key"]!);
        _iv = Encoding.UTF8.GetBytes(config["Encryption:IV"]!);
    }

    public string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;

        var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        return Convert.ToBase64String(encryptedBytes);
    }

    public string Decrypt(string cipherText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;

        var decryptor = aes.CreateDecryptor();
        var cipherBytes = Convert.FromBase64String(cipherText);
        var decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        return Encoding.UTF8.GetString(decryptedBytes);
    }
}
```

### 3. Validation des entrées

**DTOs avec validation:**

```csharp
using System.ComponentModel.DataAnnotations;

public class RegisterDto
{
    [Required(ErrorMessage = "L'email est requis")]
    [EmailAddress(ErrorMessage = "Format d'email invalide")]
    [MaxLength(200)]
    public string Email { get; set; }

    [Required(ErrorMessage = "Le mot de passe est requis")]
    [MinLength(8, ErrorMessage = "Le mot de passe doit contenir au moins 8 caractères")]
    [PasswordValidator]
    public string Password { get; set; }

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; }

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; }
}
```

**Validation dans les controllers:**

```csharp
[HttpPost("register")]
public async Task<IActionResult> Register([FromBody] RegisterDto dto)
{
    if (!ModelState.IsValid)
    {
        return BadRequest(ModelState);
    }
    
    // ... reste du code
}
```

---

## ?? Logging et Audit

### Configuration Serilog

```bash
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.File
dotnet add package Serilog.Sinks.Console
```

**Program.cs:**

```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/eduplatform-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();
```

### Événements à logger

**Sécurité:**
- ? Tentatives de connexion (succès/échec)
- ? Créations de comptes
- ? Changements de mot de passe
- ? Accès refusés (401, 403)
- ? Modifications de données sensibles

**Exemple:**

```csharp
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginDto dto)
{
    var user = _db.Users.FirstOrDefault(u => u.Email == dto.Email);

    if (user == null || !_auth.VerifyPassword(dto.Password, user.PasswordHash))
    {
        _logger.LogWarning("Failed login attempt for email: {Email}", dto.Email);
        return Unauthorized(new { message = "Email ou mot de passe incorrect" });
    }

    _logger.LogInformation("Successful login for user: {UserId}", user.Id);
    
    var token = _auth.GenerateToken(user);
    return Ok(new { token, userId = user.Id });
}
```

---

## ?? CORS (Cross-Origin Resource Sharing)

**Configuration sécurisée:**

```csharp
builder.Services.AddCors(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.AddPolicy("Development", policy =>
        {
            policy.WithOrigins(
                "https://localhost:7286",
                "http://localhost:5297"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
        });
    }
    else
    {
        options.AddPolicy("Production", policy =>
        {
            policy.WithOrigins(
                "https://eduplatform.com",
                "https://www.eduplatform.com"
            )
            .WithHeaders("Authorization", "Content-Type")
            .WithMethods("GET", "POST", "PUT", "DELETE")
            .AllowCredentials();
        });
    }
});

// Utiliser la bonne politique
var corsPolicy = app.Environment.IsDevelopment() ? "Development" : "Production";
app.UseCors(corsPolicy);
```

---

## ?? Dépendances et Packages

### Audit de sécurité

```bash
# Vérifier les vulnérabilités connues
dotnet list package --vulnerable

# Mettre à jour les packages
dotnet outdated

# Restaurer et vérifier
dotnet restore
```

### Packages recommandés

- ? `BCrypt.Net-Next` - Hashing de mots de passe
- ? `System.IdentityModel.Tokens.Jwt` - JWT
- ? `AspNetCoreRateLimit` - Rate limiting
- ? `Serilog` - Logging structuré
- ?? Éviter les packages non maintenus

---

## ?? Gestion des Secrets

### ? À NE JAMAIS FAIRE

```csharp
// NE JAMAIS mettre de secrets en dur
var apiKey = "sk-12345abcdef";
var password = "SuperSecret123!";
```

### ? Bonnes Pratiques

**1. User Secrets (Développement):**

```bash
dotnet user-secrets set "OpenAI:ApiKey" "sk-..."
```

**2. Variables d'environnement:**

```bash
export OpenAI__ApiKey="sk-..."
```

**3. Azure Key Vault (Production):**

Voir [DEPLOYMENT.md](DEPLOYMENT.md#gestion-des-secrets)

**4. Fichier .env (exclu de Git):**

```env
# .env
OPENAI_API_KEY=sk-...
JWT_KEY=...
```

**.gitignore:**

```
.env
.env.local
*.secrets.json
appsettings.*.json
!appsettings.json
!appsettings.Development.json
```

---

## ?? Checklist de Sécurité

### Authentification
- [x] JWT implémenté
- [ ] Refresh tokens
- [x] Hashing BCrypt (workFactor 12+)
- [ ] MFA (Multi-Factor Authentication)
- [ ] OAuth2/OpenID Connect

### Autorisation
- [x] Rôles définis (Student, Instructor, Admin)
- [x] Protection des endpoints
- [ ] Permissions granulaires
- [ ] Audit des accès

### Protection des Données
- [x] HTTPS obligatoire
- [ ] Chiffrement des données sensibles
- [x] Validation des entrées
- [ ] Sanitization des sorties
- [x] Protection CSRF

### Infrastructure
- [ ] Rate limiting implémenté
- [ ] WAF (Web Application Firewall)
- [ ] DDoS protection
- [ ] Monitoring de sécurité
- [ ] Backup réguliers

### Code
- [x] Secrets hors du code source
- [ ] Scan de vulnérabilités automatisé
- [ ] Dépendances à jour
- [ ] Code reviews
- [ ] Tests de sécurité

### Conformité
- [ ] RGPD compliance
- [ ] Politique de confidentialité
- [ ] Conditions d'utilisation
- [ ] Droit à l'oubli
- [ ] Export des données

---

## ?? Ressources

- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [ASP.NET Core Security](https://learn.microsoft.com/en-us/aspnet/core/security/)
- [JWT Best Practices](https://tools.ietf.org/html/rfc8725)
- [NIST Password Guidelines](https://pages.nist.gov/800-63-3/)

**Dernière mise à jour:** 2026-08-24
