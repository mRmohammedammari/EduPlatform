using EduPlatform.Web.Services;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

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

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<EduPlatform.Web.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();