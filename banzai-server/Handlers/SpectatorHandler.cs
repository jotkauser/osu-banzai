using System.Linq;
using banzai_server.Models;
using banzai_server.Services;

namespace banzai_server.Handlers;

public class SpectatorHandler
{
    private readonly SessionManager _sessions;

    public SpectatorHandler(SessionManager sessions)
    {
        _sessions = sessions;
    }

    public Task HandleStart(PlayerSession session, byte[] data)
    {
        var offset = 0;
        var targetId = PacketSerializer.ReadI32(data, ref offset);

        if (targetId == session.UserId)
            return Task.CompletedTask;

        var host = _sessions.GetByUserId(targetId);
        if (host == null)
            return Task.CompletedTask;

        if (session.SpectatingUserId.HasValue && session.SpectatingUserId.Value != targetId)
        {
            var oldHost = _sessions.GetByUserId(session.SpectatingUserId.Value);
            if (oldHost != null)
                RemoveSpectatorInternal(oldHost, session);
        }

        if (session.SpectatingUserId == targetId)
        {
            // Re-spectating same host (player downloaded the map)
            host.Enqueue(LoginResponseBuilder.SpectatorJoined((int)session.UserId));
            var joined = LoginResponseBuilder.FellowSpectatorJoined((int)session.UserId);
            foreach (var sid in host.SpectatorIds.Keys)
            {
                if (sid == session.UserId) continue;
                if (_sessions.GetByUserId(sid) is { } spec)
                    spec.Enqueue(joined);
            }
            return Task.CompletedTask;
        }

        AddSpectatorInternal(host, session);
        return Task.CompletedTask;
    }

    public Task HandleStop(PlayerSession session, byte[] data)
    {
        if (!session.SpectatingUserId.HasValue)
            return Task.CompletedTask;

        var host = _sessions.GetByUserId(session.SpectatingUserId.Value);
        if (host != null)
            RemoveSpectatorInternal(host, session);
        else
            session.SpectatingUserId = null;

        return Task.CompletedTask;
    }

    public Task HandleFrames(PlayerSession session, byte[] data)
    {
        if (session.SpectatorIds.Count > 0)
        {
            var framePacket = LoginResponseBuilder.SpectateFrames(data);
            _sessions.EnqueueToSpectators(session.UserId, framePacket);
        }
        return Task.CompletedTask;
    }

    public Task HandleCantSpectate(PlayerSession session, byte[] data)
    {
        if (!session.SpectatingUserId.HasValue)
            return Task.CompletedTask;

        var host = _sessions.GetByUserId(session.SpectatingUserId.Value);
        if (host == null)
            return Task.CompletedTask;

        var cantPacket = LoginResponseBuilder.SpectatorCantSpectate((int)session.UserId);
        host.Enqueue(cantPacket);
        _sessions.EnqueueToSpectators(host.UserId, cantPacket);
        return Task.CompletedTask;
    }

    public void HandleDisconnect(PlayerSession session)
    {
        if (session.SpectatingUserId.HasValue)
        {
            var host = _sessions.GetByUserId(session.SpectatingUserId.Value);
            if (host != null)
                RemoveSpectatorInternal(host, session);
            else
                session.SpectatingUserId = null;
        }

        if (session.SpectatorIds.Count > 0)
        {
            var channelName = $"#spec_{session.UserId}";
            var spectatorIds = session.SpectatorIds.Keys.ToList();
            foreach (var sid in spectatorIds)
            {
                if (_sessions.GetByUserId(sid) is { } spec)
                {
                    spec.SpectatingUserId = null;
                    spec.Enqueue(LoginResponseBuilder.SpectatorLeft((int)session.UserId));
                }
                _sessions.LeaveChannel(channelName, sid);
            }
            session.SpectatorIds.Clear();
            _sessions.LeaveChannel(channelName, session.UserId);
        }
    }

    private void AddSpectatorInternal(PlayerSession host, PlayerSession spectator)
    {
        var channelName = $"#spec_{host.UserId}";

        if (host.SpectatorIds.Count == 0)
        {
            _sessions.JoinChannel(channelName, host);
            host.Enqueue(LoginResponseBuilder.ChannelJoinSuccess("#spectator"));
        }

        _sessions.JoinChannel(channelName, spectator);
        spectator.Enqueue(LoginResponseBuilder.ChannelJoinSuccess("#spectator"));

        host.Enqueue(LoginResponseBuilder.SpectatorJoined((int)spectator.UserId));

        var newSpecJoined = LoginResponseBuilder.FellowSpectatorJoined((int)spectator.UserId);
        foreach (var sid in host.SpectatorIds.Keys)
        {
            if (_sessions.GetByUserId(sid) is { } existingSpec)
            {
                existingSpec.Enqueue(newSpecJoined);
                spectator.Enqueue(LoginResponseBuilder.FellowSpectatorJoined((int)sid));
            }
        }

        host.SpectatorIds.TryAdd(spectator.UserId, 0);
        spectator.SpectatingUserId = host.UserId;

        var count = host.SpectatorIds.Count + 1;
        var chanInfo = LoginResponseBuilder.ChannelInfo("#spectator", $"{host.Username}'s spectator channel", count);
        _sessions.EnqueueToChannel(channelName, chanInfo);
    }

    private void RemoveSpectatorInternal(PlayerSession host, PlayerSession spectator)
    {
        var channelName = $"#spec_{host.UserId}";

        host.SpectatorIds.TryRemove(spectator.UserId, out _);
        spectator.SpectatingUserId = null;

        _sessions.LeaveChannel(channelName, spectator.UserId);

        host.Enqueue(LoginResponseBuilder.SpectatorLeft((int)spectator.UserId));

        if (host.SpectatorIds.Count == 0)
        {
            _sessions.LeaveChannel(channelName, host.UserId);
            host.Enqueue(LoginResponseBuilder.ChannelKick("#spectator"));
        }
        else
        {
            var leftPacket = LoginResponseBuilder.FellowSpectatorLeft((int)spectator.UserId);
            _sessions.EnqueueToChannel(channelName, leftPacket);

            var count = host.SpectatorIds.Count + 1;
            var chanInfo = LoginResponseBuilder.ChannelInfo("#spectator", $"{host.Username}'s spectator channel", count);
            _sessions.EnqueueToChannel(channelName, chanInfo);
        }
    }
}
