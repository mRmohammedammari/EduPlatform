using System.Diagnostics;

namespace EduPlatform.API.Middleware;

public class RequestCorrelationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestCorrelationMiddleware> _logger;

    public RequestCorrelationMiddleware(
        RequestDelegate next,
        ILogger<RequestCorrelationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue("X-Correlation-Id", out var header)
            ? header.ToString()
            : Guid.NewGuid().ToString("N");
        context.Response.Headers["X-Correlation-Id"] = correlationId;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "HTTP {Method} {Path} -> {StatusCode} in {ElapsedMs}ms; CorrelationId={CorrelationId}; UserId={UserId}; SessionId={SessionId}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                correlationId,
                context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous",
                context.Request.Headers["X-Session-Id"].ToString());
        }
    }
}