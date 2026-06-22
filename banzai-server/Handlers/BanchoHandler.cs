using banzai_server.Models;
using banzai_server.Services;
using Microsoft.EntityFrameworkCore;

namespace banzai_server.Handlers;

public class BanchoHandler
{
    private readonly BanzaiDbContext _db;
    private readonly ISessionStore _store;
    private readonly SessionManager _sessions;

    public BanchoHandler(BanzaiDbContext db, ISessionStore store, SessionManager sessions)
    {
        _db = db;
        _store = store;
        _sessions = sessions;
    }

    public async Task Handle(HttpContext ctx)
    {
        var token = ctx.Request.Headers["osu-token"].FirstOrDefault();

        if (string.IsNullOrEmpty(token))
            await HandleLogin(ctx);
        else
            await HandlePoll(ctx, token);
    }

    private async Task HandleLogin(HttpContext ctx)
    {
        using var reader = new StreamReader(ctx.Request.Body);
        var username = (await reader.ReadLineAsync())?.Trim();
        var passwordMd5 = (await reader.ReadLineAsync())?.Trim();

        // TODO: parse client_info (osu_version|utc_offset|display_city|client_hashes|pm_private)

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(passwordMd5))
        {
            await WriteLoginFailure(ctx);
            return;
        }

        var user = await _db.Users
            .Include(u => u.UserStats)
            .FirstOrDefaultAsync(u => u.Name == username);

        if (user == null || !BCrypt.Net.BCrypt.Verify(passwordMd5, user.Password))
        {
            await WriteLoginFailure(ctx);
            return;
        }

        var token = Guid.NewGuid().ToString("n");
        await _store.SetSession(token, user.Id.ToString());

        var session = new PlayerSession(token, user.Id, user.Name, user.Privileges);
        _sessions.Add(session);

        ctx.Response.Headers["cho-token"] = token;
        ctx.Response.ContentType = "application/octet-stream";

        var loginPackets = await LoginResponseBuilder.Build(user, _sessions, _db);
        foreach (var packet in loginPackets)
            await PacketSerializer.WritePacketAsync(ctx.Response.Body, packet);

        var channels = await _db.ChatChannels.ToListAsync();
        foreach (var ch in channels)
        {
            _sessions.JoinChannel(ch.Name, session);
            var count = _sessions.GetChannelCount(ch.Name);
            var update = LoginResponseBuilder.ChannelInfo(ch.Name, ch.Description, count);
            _sessions.EnqueueToChannel(ch.Name, update);
        }
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
                    // Broadcast updated channel counts
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
                    await HandleChannelJoin(session, packet.Data);
                    break;
                case 78:
                    await HandleChannelPart(session, packet.Data);
                    break;
                case 1:
                    await HandlePublicMessage(session, packet.Data);
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

    private async Task HandlePublicMessage(PlayerSession session, byte[] data)
    {
        try
        {
            var (_, text, recipient, _) = PacketSerializer.ReadMessage(data);

            var trimmed = text.Trim();
            if (string.IsNullOrEmpty(trimmed)) return;

            if (trimmed.Length > 2000)
                trimmed = trimmed[..2000];

            var channel = await _db.ChatChannels.FirstOrDefaultAsync(c => c.Name == recipient);

            _db.ChatMessages.Add(new ChatMessage
            {
                FromId = session.UserId,
                ChannelId = channel?.Id,
                Message = trimmed,
                CreatedAt = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync();

            var msgPacket = LoginResponseBuilder.SendMessage(session.Username, trimmed, recipient, (int)session.UserId);
            _sessions.EnqueueToChannel(recipient, msgPacket, exceptUserId: session.UserId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[chat] {ex.Message}");
        }
    }

    private static string ReadChannelName(byte[] data)
    {
        var offset = 0;
        return PacketSerializer.ReadString(data, ref offset);
    }

    private async Task HandleChannelJoin(PlayerSession session, byte[] data)
    {
        var channelName = ReadChannelName(data);
        var channel = await _db.ChatChannels.FirstOrDefaultAsync(c => c.Name == channelName);
        if (channel == null) return;

        _sessions.JoinChannel(channelName, session);
        session.Enqueue(LoginResponseBuilder.ChannelJoinSuccess(channelName));

        var count = _sessions.GetChannelCount(channelName);
        var update = LoginResponseBuilder.ChannelInfo(channelName, channel.Description, count);
        _sessions.EnqueueToChannel(channelName, update);
    }

    private async Task HandleChannelPart(PlayerSession session, byte[] data)
    {
        var channelName = ReadChannelName(data);
        _sessions.LeaveChannel(channelName, session.UserId);

        var channel = await _db.ChatChannels.FirstOrDefaultAsync(c => c.Name == channelName);
        if (channel == null) return;

        var count = _sessions.GetChannelCount(channelName);
        var update = LoginResponseBuilder.ChannelInfo(channelName, channel.Description, count);
        _sessions.EnqueueToChannel(channelName, update);
    }

    private async Task WriteLoginFailure(HttpContext ctx)
    {
        ctx.Response.Headers["cho-token"] = "incorrect-credentials";
        ctx.Response.ContentType = "application/octet-stream";
        foreach (var packet in LoginResponseBuilder.Failure())
            await PacketSerializer.WritePacketAsync(ctx.Response.Body, packet);
    }
}
