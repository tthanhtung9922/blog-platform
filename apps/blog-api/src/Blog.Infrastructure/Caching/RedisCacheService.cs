using Blog.Application.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace Blog.Infrastructure.Caching;

public class RedisCacheService(
    IConnectionMultiplexer connection,
    ILogger<RedisCacheService> logger) : IRedisCacheService
{
    // Lua script: SCAN cursor loop — non-blocking, atomic key discovery and deletion.
    // Uses ARGV[1] for the pattern (not KEYS[]) so Redis treats it as a value, not a key binding.
    // NEVER use 'KEYS pattern' — it is O(N) and blocks the Redis event loop.
    private const string RemoveByPatternScript = @"
        local cursor = '0'
        repeat
            local result = redis.call('SCAN', cursor, 'MATCH', ARGV[1], 'COUNT', 100)
            cursor = result[1]
            local keys = result[2]
            for _, key in ipairs(keys) do
                redis.call('DEL', key)
            end
        until cursor == '0'
        return 1
    ";

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var db = connection.GetDatabase();
        var value = await db.StringGetAsync(key);
        if (!value.HasValue) return default;
        try
        {
            return JsonSerializer.Deserialize<T>((string)value!);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize cached value for key {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        var db = connection.GetDatabase();
        var serialized = JsonSerializer.Serialize(value);
        await db.StringSetAsync(key, serialized, ttl);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        var db = connection.GetDatabase();
        await db.KeyDeleteAsync(key);
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken ct = default)
    {
        var db = connection.GetDatabase();
        // ScriptEvaluateAsync with values: (ARGV) not keys: (KEYS) — pattern is a value, not a Redis key binding.
        await db.ScriptEvaluateAsync(RemoveByPatternScript, values: new RedisValue[] { pattern });
    }
}
