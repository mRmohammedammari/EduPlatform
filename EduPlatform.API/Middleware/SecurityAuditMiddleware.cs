using System.Diagnostics;
using System.Security.Claims;

namespace EduPlatform.API.Middleware;

public sealed class SecurityAuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityAuditMiddleware> _logger;

    public SecurityAuditMiddleware(
        RequestDelegate next,
        ILogger<SecurityAuditMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            _logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path}; StatusCode=500; CorrelationId={CorrelationId}; UserId={UserId}; RemoteIp={RemoteIp}; ElapsedMs={ElapsedMs}",
                context.Request.Method,
                context.Request.Path,
                context.Response.Headers["X-Correlation-Id"].ToString(),
                GetUserId(context),
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                stopwatch.ElapsedMilliseconds);
            throw;
        }

        stopwatch.Stop();
        var statusCode = context.Response.StatusCode;
        var correlationId = context.Response.Headers["X-Correlation-Id"].ToString();
        var userId = GetUserId(context);
        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (statusCode is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden)
        {
            _logger.LogWarning(
                "Security event {EventType} for {Method} {Path}; StatusCode={StatusCode}; CorrelationId={CorrelationId}; UserId={UserId}; RemoteIp={RemoteIp}; ElapsedMs={ElapsedMs}",
                statusCode == StatusCodes.Status401Unauthorized ? "AuthenticationFailure" : "AuthorizationFailure",
                context.Request.Method,
                context.Request.Path,
                statusCode,
                correlationId,
                userId,
                remoteIp,
                stopwatch.ElapsedMilliseconds);
        }
        else if (statusCode >= 500)
        {
            _logger.LogError(
                "Server error for {Method} {Path}; StatusCode={StatusCode}; CorrelationId={CorrelationId}; UserId={UserId}; RemoteIp={RemoteIp}; ElapsedMs={ElapsedMs}",
                context.Request.Method,
                context.Request.Path,
                statusCode,
                correlationId,
                userId,
                remoteIp,
                stopwatch.ElapsedMilliseconds);
        }
        else if (stopwatch.ElapsedMilliseconds >= 2000)
        {
            _logger.LogWarning(
                "Slow request {Method} {Path}; StatusCode={StatusCode}; CorrelationId={CorrelationId}; UserId={UserId}; RemoteIp={RemoteIp}; ElapsedMs={ElapsedMs}",
                context.Request.Method,
                context.Request.Path,
                statusCode,
                correlationId,
                userId,
                remoteIp,
                stopwatch.ElapsedMilliseconds);
        }
    }

    private static string GetUserId(HttpContext context)
    {
        return context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
    }
}
