using EduPlatform.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Sockets;

namespace EduPlatform.API.Controllers;

[ApiController]
[Route("health")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly EduDbContext _db;
    private readonly IConfiguration _configuration;

    public HealthController(
        EduDbContext db,
        IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    [HttpGet("details")]
    public async Task<IActionResult> Details(CancellationToken cancellationToken)
    {
        var checks = new Dictionary<string, object>();
        var healthy = true;

        checks["sqlServer"] = await CheckAsync(
            () => _db.Database.CanConnectAsync(cancellationToken));
        checks["cassandra"] = await CheckTcpAsync(
            _configuration["Cassandra:Host"] ?? "localhost",
            GetPort("Cassandra:Port", 9042),
            cancellationToken);
        var redisParts = (_configuration["Redis:ConnectionString"] ?? "localhost:6379")
            .Split(':', 2);
        checks["redis"] = await CheckTcpAsync(
            redisParts[0],
            redisParts.Length == 2 && int.TryParse(redisParts[1], out var redisPort)
                ? redisPort
                : 6379,
            cancellationToken);
        checks["kafka"] = await CheckKafkaAsync(cancellationToken);

        foreach (var check in checks.Values.OfType<Dictionary<string, object>>())
        {
            if (check.TryGetValue("status", out var status) &&
                string.Equals(status?.ToString(), "unhealthy", StringComparison.Ordinal))
            {
                healthy = false;
            }
        }

        var result = new
        {
            status = healthy ? "healthy" : "degraded",
            timestamp = DateTime.UtcNow,
            checks
        };

        return healthy ? Ok(result) : StatusCode(StatusCodes.Status503ServiceUnavailable, result);
    }

    private static async Task<Dictionary<string, object>> CheckAsync(
        Func<Task<bool>> check)
    {
        try
        {
            await check();
            return new Dictionary<string, object>
            {
                ["status"] = "healthy"
            };
        }
        catch (Exception exception)
        {
            return new Dictionary<string, object>
            {
                ["status"] = "unhealthy",
                ["error"] = exception.Message
            };
        }
    }

    private async Task<Dictionary<string, object>> CheckTcpAsync(
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, cancellationToken);
            return new Dictionary<string, object> { ["status"] = "healthy" };
        }
        catch (Exception exception)
        {
            return new Dictionary<string, object>
            {
                ["status"] = "unhealthy",
                ["error"] = exception.Message
            };
        }
    }

    private int GetPort(string key, int fallback)
    {
        return int.TryParse(_configuration[key], out var port) ? port : fallback;
    }

    private async Task<Dictionary<string, object>> CheckKafkaAsync(
        CancellationToken cancellationToken)
    {
        var bootstrap = _configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
        var parts = bootstrap.Split(':', 2);
        var host = parts[0];
        var port = parts.Length == 2 && int.TryParse(parts[1], out var configuredPort)
            ? configuredPort
            : 9092;

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, cancellationToken);
            return new Dictionary<string, object>
            {
                ["status"] = "healthy"
            };
        }
        catch (Exception exception)
        {
            return new Dictionary<string, object>
            {
                ["status"] = "unhealthy",
                ["error"] = exception.Message
            };
        }
    }
}
