using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EduPlatform.Data.Cache
{
    public class CacheService
    {
        private readonly IDatabase _db;
        private readonly TimeSpan _defaultExpiry = TimeSpan.FromMinutes(15);
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };

        public CacheService(IConfiguration config)
        {
            var options = ConfigurationOptions.Parse(
                config["Redis:ConnectionString"] ?? "localhost:6379");
            options.AbortOnConnectFail = false;
            var connection = ConnectionMultiplexer.Connect(options);
            _db = connection.GetDatabase();
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            try
            {
                var json = JsonSerializer.Serialize(value, _jsonOptions);
                await _db.StringSetAsync(key, json, expiry ?? _defaultExpiry);
            }
            catch
            {
            }
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            try
            {
                var value = await _db.StringGetAsync(key);
                if (value.IsNullOrEmpty) return default;
                return JsonSerializer.Deserialize<T>(value!, _jsonOptions);
            }
            catch
            {
                return default;
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                await _db.KeyDeleteAsync(key);
            }
            catch
            {
            }
        }

        public Task<TimeSpan> PingAsync()
        {
            return _db.PingAsync();
        }

        public static string CourseKey(Guid id) => $"course:{id}";
        public static string UserProgressKey(Guid userId, Guid courseId)
            => $"progress:{userId}:{courseId}";
        // v2 : la liste de cours expose desormais vignette, compteurs et notes.
        // Le suffixe evite de relire une entree au format precedent apres deploiement.
        public static string CourseListKey() => "courses:all:v2";
        public static string CourseStatsKey() => "courses:stats";
        public static string CourseCategoriesKey() => "courses:categories";
    }
}