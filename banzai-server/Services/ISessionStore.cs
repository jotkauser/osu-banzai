namespace banzai_server.Services;

public interface ISessionStore
{
    Task<string?> GetUserId(string token);
    Task SetSession(string token, string userId, TimeSpan? ttl = null);
    Task RemoveSession(string token);
}
