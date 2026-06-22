using banzai_server.Models;
using banzai_server.Services;
using Microsoft.EntityFrameworkCore;

namespace banzai_server.Handlers;

public class BanchoHandler
{
    private readonly BanzaiDbContext _db;
    private readonly ISessionStore _store;
    private readonly SessionManager _sessions;
    private readonly LoginHandler _login;
    private readonly ChatHandler _chat;
    private readonly SpectatorHandler _spectator;

    public BanchoHandler(BanzaiDbContext db, ISessionStore store, SessionManager sessions,
        LoginHandler login, ChatHandler chat, SpectatorHandler spectator)
    {
        _db = db;
        _store = store;
        _sessions = sessions;
        _login = login;
        _chat = chat;
        _spectator = spectator;
    }

    public async Task Handle(HttpContext ctx)
    {
        var token = ctx.Request.Headers["osu-token"].FirstOrDefault();

        if (string.IsNullOrEmpty(token))
            await _login.Handle(ctx);
        else
            await HandlePoll(ctx, token);
    }

    private async Task HandlePoll(HttpContext ctx, string token)
    {
        if (_sessions.GetByToken(token) is not { } session)
        {
            ctx.Response.ContentType = "application/octet-stream";
            await PacketSerializer.WritePacketAsync(ctx.Response.Body, LoginResponseBuilder.Restart());
            return;
        }

        session.LastRequest = DateTime.UtcNow;
        await _store.SetSession(token, session.UserId.ToString());

        // Reap idle sessions
        var cutoff = DateTime.UtcNow.AddSeconds(-60);
        var idle = _sessions.OnlinePlayers.Where(p => p.LastRequest < cutoff).ToList();
        foreach (var p in idle)
        {
            _sessions.Remove(p);
            _sessions.LeaveAllChannels(p.UserId);
            _spectator.HandleDisconnect(p);
            await _store.RemoveSession(p.Token);
            _sessions.EnqueueToAll(LoginResponseBuilder.Logout(p.UserId));
        }

        using var ms = new MemoryStream();
        await ctx.Request.Body.CopyToAsync(ms);
        ms.Position = 0;
        var packets = PacketSerializer.ReadPackets(ms);

        foreach (var packet in packets)
        {
            switch (packet.Id)
            {
                case 4:
                    break;
                case 0:
                    HandleChangeAction(session, packet.Data);
                    break;
                case 2:
                    _sessions.Remove(session);
                    _sessions.LeaveAllChannels(session.UserId);
                    _spectator.HandleDisconnect(session);
                    await _store.RemoveSession(token);
                    _sessions.EnqueueToAll(LoginResponseBuilder.Logout(session.UserId));
                    var channels = await _db.ChatChannels.ToListAsync();
                    foreach (var ch in channels)
                    {
                        var count = _sessions.GetChannelCount(ch.Name);
                        var update = BanchoPackets.ChannelInfo(ch.Name, ch.Description, count);
                        _sessions.EnqueueToChannel(ch.Name, update);
                    }
                    ctx.Response.ContentType = "application/octet-stream";
                    return;
                case 3:
                    session.Enqueue(BanchoPackets.Stats(session));
                    break;
                case 63:
                    await _chat.HandleJoin(session, packet.Data);
                    break;
                case 78:
                    await _chat.HandlePart(session, packet.Data);
                    break;
                case 1:
                    await _chat.HandlePublicMessage(session, packet.Data);
                    break;
                case 16:
                    await _spectator.HandleStart(session, packet.Data);
                    break;
                case 17:
                    await _spectator.HandleStop(session, packet.Data);
                    break;
                case 18:
                    await _spectator.HandleFrames(session, packet.Data);
                    break;
                case 21:
                    await _spectator.HandleCantSpectate(session, packet.Data);
                    break;
                case 73:
                    await HandleFriendAdd(session, packet.Data);
                    break;
                case 74:
                    await HandleFriendRemove(session, packet.Data);
                    break;
                default:
                    break;
            }
        }

        ctx.Response.ContentType = "application/octet-stream";
        var outbound = session.DrainOutbound();
        foreach (var packet in outbound)
            await PacketSerializer.WritePacketAsync(ctx.Response.Body, packet);
    }

    private async Task HandleFriendAdd(PlayerSession session, byte[] data)
    {
        var offset = 0;
        var friendId = PacketSerializer.ReadI32(data, ref offset);

        if (friendId == session.UserId)
            return;

        var exists = await _db.UserFriends.AnyAsync(f => f.UserId == session.UserId && f.FriendId == friendId);
        if (!exists)
        {
            _db.UserFriends.Add(new UserFriend { UserId = session.UserId, FriendId = friendId });
            await _db.SaveChangesAsync();
        }

        // Send the friend's current presence if they're online
        if (_sessions.GetByUserId(friendId) is { } friend)
        {
            session.Enqueue(BanchoPackets.Presence(friend.UserId, friend.Username, friend.Privileges, friend.UtcOffset));
            session.Enqueue(BanchoPackets.Stats(friend));
        }
    }

    private async Task HandleFriendRemove(PlayerSession session, byte[] data)
    {
        var offset = 0;
        var friendId = PacketSerializer.ReadI32(data, ref offset);

        var row = await _db.UserFriends
            .FirstOrDefaultAsync(f => f.UserId == session.UserId && f.FriendId == friendId);
        if (row != null)
        {
            _db.UserFriends.Remove(row);
            await _db.SaveChangesAsync();
        }
    }

    private void HandleChangeAction(PlayerSession session, byte[] data)
    {
        var offset = 0;
        session.Action = PacketSerializer.ReadU8(data, ref offset);
        session.InfoText = PacketSerializer.ReadString(data, ref offset);
        session.MapMd5 = PacketSerializer.ReadString(data, ref offset);
        session.Mods = PacketSerializer.ReadI32(data, ref offset);
        session.Mode = PacketSerializer.ReadU8(data, ref offset);
        session.MapId = PacketSerializer.ReadI32(data, ref offset);

        var statsPacket = BanchoPackets.Stats(session);
        _sessions.EnqueueToAllExcept(statsPacket, session.UserId);
    }
}
