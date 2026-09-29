using EduPlatform.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using EduPlatform.Web.Logging;

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

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<AuthStateService>();

builder.Services.AddHttpClient("EduPlatformAPI", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5053/");
});

var app = builder.Build();

// Trust reverse-proxy headers (X-Forwarded-Proto/For) when behind Nginx/Traefik in production.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
        context.Response.Headers["X-Permitted-Cross-Domain-Policies"] = "none";
        if (!context.Response.Headers.ContainsKey("Cache-Control"))
        {
            context.Response.Headers["Cache-Control"] = "no-store, max-age=0";
        }

        return Task.CompletedTask;
    });

    await next();
});

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<EduPlatform.Web.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();