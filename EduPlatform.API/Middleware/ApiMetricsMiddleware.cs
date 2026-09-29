using System.Diagnostics;
using EduPlatform.API.Monitoring;

namespace EduPlatform.API.Middleware;

public sealed class ApiMetricsMiddleware
{
    private readonly RequestDelegate _next;

    public ApiMetricsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ApiMetrics metrics)
    {
        metrics.RequestStarted();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            metrics.RequestCompleted(context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
        }
    }
}