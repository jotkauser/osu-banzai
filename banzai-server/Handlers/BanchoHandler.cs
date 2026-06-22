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

    public BanchoHandler(BanzaiDbContext db, ISessionStore store, SessionManager sessions,
        LoginHandler login, ChatHandler chat)
    {
        _db = db;
        _store = store;
        _sessions = sessions;
        _login = login;
        _chat = chat;
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
                case 2:
                    _sessions.Remove(session);
                    _sessions.LeaveAllChannels(session.UserId);
                    await _store.RemoveSession(token);
                    _sessions.EnqueueToAll(LoginResponseBuilder.Logout(session.UserId));
                    var channels = await _db.ChatChannels.ToListAsync();
                    foreach (var ch in channels)
                    {
                        var count = _sessions.GetChannelCount(ch.Name);
                        var update = LoginResponseBuilder.ChannelInfo(ch.Name, ch.Description, count);
                        _sessions.EnqueueToChannel(ch.Name, update);
                    }
                    ctx.Response.ContentType = "application/octet-stream";
                    return;
                case 3:
                    session.Enqueue(LoginResponseBuilder.Stats(session.UserId, null));
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
                default:
                    break;
            }
        }

        ctx.Response.ContentType = "application/octet-stream";
        var outbound = session.DrainOutbound();
        foreach (var packet in outbound)
            await PacketSerializer.WritePacketAsync(ctx.Response.Body, packet);
    }
}
