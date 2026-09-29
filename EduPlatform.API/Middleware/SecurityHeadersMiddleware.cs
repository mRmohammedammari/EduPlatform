namespace EduPlatform.API.Middleware;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-Permitted-Cross-Domain-Policies"] = "none";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

        if (!context.Response.Headers.ContainsKey("Cache-Control"))
        {
            context.Response.Headers["Cache-Control"] = "no-store, max-age=0";
        }

        if (!context.Response.Headers.ContainsKey("X-Download-Options"))
        {
            context.Response.Headers["X-Download-Options"] = "noopen";
        }

        await _next(context);
    }
}
