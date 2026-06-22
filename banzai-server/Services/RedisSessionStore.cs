using StackExchange.Redis;

namespace banzai_server.Services;

public class RedisSessionStore : ISessionStore, IDisposable
{
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    public RedisSessionStore(string host, int port)
    {
        _redis = ConnectionMultiplexer.Connect($"{host}:{port}");
        _db = _redis.GetDatabase();
    }

    public async Task<string?> GetUserId(string token)
    {
        var key = $"bancho:session:{token}";
        var value = await _db.StringGetAsync(key);

        if (value.IsNull)
            return null;

        await _db.KeyExpireAsync(key, DefaultTtl);
        return value.ToString();
    }

    public async Task SetSession(string token, string userId, TimeSpan? ttl = null)
    {
        var key = $"bancho:session:{token}";
        await _db.StringSetAsync(key, userId, ttl ?? DefaultTtl);
    }

    public async Task RemoveSession(string token)
    {
        var key = $"bancho:session:{token}";
        await _db.KeyDeleteAsync(key);
    }

    public void Dispose() => _redis.Dispose();
}
