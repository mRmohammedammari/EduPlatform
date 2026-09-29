using EduPlatform.API.Services;
using EduPlatform.API.Middleware;
using EduPlatform.Core.Services;
using EduPlatform.Data.Cache;
using EduPlatform.Data.Cassandra;
using EduPlatform.Data.Cassandra.Repositories;
using EduPlatform.Data.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.RateLimiting;
using EduPlatform.API.Logging;
using EduPlatform.API.Monitoring;
using EduPlatform.API.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddProvider(new FileLoggerProvider(Path.Combine(AppContext.BaseDirectory, "Logs")));

var dataProtectionPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionPath))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));
}

var sqlConnection = builder.Configuration.GetConnectionString("SqlServer");
if (string.IsNullOrWhiteSpace(sqlConnection))
{
    builder.Configuration["ConnectionStrings:SqlServer"] =
        "Server=(localdb)\\MSSQLLocalDB;Database=EduPlatformDB;Trusted_Connection=True;TrustServerCertificate=True";
}

if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Key"]) && builder.Environment.IsDevelopment())
{
    builder.Configuration["Jwt:Key"] = "EduPlatformDevelopmentSecretKeyChangeMe2026!";
}

if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Issuer"]))
{
    builder.Configuration["Jwt:Issuer"] = "EduPlatform";
}

if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Audience"]))
{
    builder.Configuration["Jwt:Audience"] = "EduPlatformUsers";
}

if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:ExpirationHours"]))
{
    builder.Configuration["Jwt:ExpirationHours"] = "24";
}

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be configured with at least 32 characters.");

// CORS - autoriser le frontend Blazor
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazor", policy =>
    {
        policy.WithOrigins(
            "https://localhost:7286",
            "http://localhost:5297",
            "https://localhost:7194",
            "http://localhost:5053")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// SQL Server
builder.Services.AddDbContext<EduDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));

// Cassandra
builder.Services.AddSingleton<CassandraContext>();
builder.Services.AddSingleton<ActivityRepository>();
builder.Services.AddSingleton<TestResultRepository>();

// Redis
builder.Services.AddSingleton<CacheService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<IPaymentGateway, DemoPaymentGateway>();

// BigData Services
builder.Services.AddSingleton<EduPlatform.BigData.Kafka.EventProducer>();
builder.Services.AddScoped<EduPlatform.BigData.Analytics.AnalyticsService>();
builder.Services.AddScoped<EduPlatform.BigData.Analytics.RecommendationService>();
builder.Services.AddHostedService<EduPlatform.BigData.Kafka.ActivityConsumer>();

// Chatbot
builder.Services.AddHttpClient("openai");
builder.Services.AddScoped<ChatbotService>();
builder.Services.AddSingleton<ChatRepository>();

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/notifications"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "Authentification requise."
                });
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "Accès interdit."
                });
            }
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<ApiMetrics>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Trop de requêtes. Réessayez dans une minute." },
            cancellationToken);
    };
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var path = httpContext.Request.Path;
        if (path.StartsWithSegments("/health") || path.StartsWithSegments("/metrics"))
        {
            return RateLimitPartition.GetNoLimiter("monitoring");
        }

        var clientKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            clientKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

// Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "EduPlatform API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Entrer : Bearer {votre_token}"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                    { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Trust reverse-proxy headers (X-Forwarded-Proto/For) when behind Nginx/Traefik in production.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
// The reverse proxy runs on the internal Docker network, not loopback, so the default
// KnownProxies/KnownNetworks (loopback-only) would reject its forwarded headers.
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

if (builder.Configuration.GetValue<bool>("Database:ApplyMigrations"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<EduDbContext>();
    db.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseStaticFiles();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseRateLimiter();
app.UseMiddleware<ApiMetricsMiddleware>();
app.UseMiddleware<RequestCorrelationMiddleware>();
app.UseMiddleware<SecurityAuditMiddleware>();
app.UseCors("AllowBlazor");
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapHub<NotificationsHub>("/hubs/notifications");
app.MapGet("/metrics", (ApiMetrics metrics) =>
{
    return Results.Text(metrics.ToPrometheus(), "text/plain; version=0.0.4; charset=utf-8");
}).AllowAnonymous();
app.MapControllers();

app.Run();