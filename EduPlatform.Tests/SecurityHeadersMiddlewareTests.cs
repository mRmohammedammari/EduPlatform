using System.Net;
using EduPlatform.API.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EduPlatform.Tests;

public class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_SetsBaselineSecurityHeaders()
    {
        var context = new DefaultHttpContext();
        var next = new RequestDelegate(_ => Task.CompletedTask);
        var middleware = new SecurityHeadersMiddleware(next);

        await middleware.InvokeAsync(context);

        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"].ToString());
        Assert.Equal("strict-origin-when-cross-origin", context.Response.Headers["Referrer-Policy"].ToString());
        Assert.Equal("geolocation=(), microphone=(), camera=()", context.Response.Headers["Permissions-Policy"].ToString());
    }
}
