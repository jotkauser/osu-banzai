using System.Collections.Concurrent;
using banzai_server.Models;

namespace banzai_server.Services;

public class SessionManager
{
    private readonly ConcurrentDictionary<string, PlayerSession> _byToken = new();
    private readonly ConcurrentDictionary<long, PlayerSession> _byUserId = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<long, byte>> _channels = new();

    public PlayerSession? GetByToken(string token)
    {
        _byToken.TryGetValue(token, out var session);
        return session;
    }

    public PlayerSession? GetByUserId(long userId)
    {
        _byUserId.TryGetValue(userId, out var session);
        return session;
    }

    public IReadOnlyCollection<PlayerSession> OnlinePlayers => _byUserId.Values.ToList();

    public void Add(PlayerSession session)
    {
        _byToken[session.Token] = session;
        _byUserId[session.UserId] = session;
    }

    public void Remove(PlayerSession session)
    {
        _byToken.TryRemove(session.Token, out _);
        _byUserId.TryRemove(session.UserId, out _);
    }

    public void JoinChannel(string channel, PlayerSession session)
    {
        var members = _channels.GetOrAdd(channel, _ => new ConcurrentDictionary<long, byte>());
        members[session.UserId] = 0;
    }

    public void LeaveChannel(string channel, long userId)
    {
        if (_channels.TryGetValue(channel, out var members))
            members.TryRemove(userId, out _);
    }

    public void LeaveAllChannels(long userId)
    {
        foreach (var (name, members) in _channels)
            members.TryRemove(userId, out _);
    }

    public int GetChannelCount(string channel)
    {
        return _channels.TryGetValue(channel, out var members) ? members.Count : 0;
    }

    public void EnqueueToChannel(string channel, BanchoPacket packet, long? exceptUserId = null)
    {
        if (!_channels.TryGetValue(channel, out var members)) return;

        foreach (var uid in members.Keys)
        {
            if (uid == exceptUserId) continue;
            if (_byUserId.TryGetValue(uid, out var session))
                session.Enqueue(packet);
        }
    }

    public void EnqueueToAll(BanchoPacket packet)
    {
        foreach (var session in _byUserId.Values)
            session.Enqueue(packet);
    }

    public void EnqueueToAllExcept(BanchoPacket packet, long exceptUserId)
    {
        foreach (var kv in _byUserId)
            if (kv.Key != exceptUserId)
                kv.Value.Enqueue(packet);
    }

    public void EnqueueToSpectators(long hostId, BanchoPacket packet, long? exceptUserId = null)
    {
        if (!_byUserId.TryGetValue(hostId, out var host)) return;
        foreach (var sid in host.SpectatorIds.Keys)
        {
            if (sid == exceptUserId) continue;
            if (_byUserId.TryGetValue(sid, out var spec))
                spec.Enqueue(packet);
        }
    }

    public int GetSpectatorCount(long hostId)
    {
        if (!_byUserId.TryGetValue(hostId, out var host)) return 0;
        return host.SpectatorIds.Count;
    }
}
