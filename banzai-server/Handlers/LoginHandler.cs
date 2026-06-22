using banzai_server.Models;
using banzai_server.Services;
using Microsoft.EntityFrameworkCore;

namespace banzai_server.Handlers;

public class LoginHandler
{
    private readonly BanzaiDbContext _db;
    private readonly ISessionStore _store;
    private readonly SessionManager _sessions;

    public LoginHandler(BanzaiDbContext db, ISessionStore store, SessionManager sessions)
    {
        _db = db;
        _store = store;
        _sessions = sessions;
    }

    public async Task Handle(HttpContext ctx)
    {
        using var reader = new StreamReader(ctx.Request.Body);
        var username = (await reader.ReadLineAsync())?.Trim();
        var passwordMd5 = (await reader.ReadLineAsync())?.Trim();

        // TODO: parse client_info (osu_version|utc_offset|display_city|client_hashes|pm_private)

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(passwordMd5))
        {
            await WriteFailure(ctx);
            return;
        }

        var user = await _db.Users
            .Include(u => u.UserStats)
            .FirstOrDefaultAsync(u => u.Name == username);

        if (user == null || !BCrypt.Net.BCrypt.Verify(passwordMd5, user.Password))
        {
            await WriteFailure(ctx);
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

        // Broadcast new player to existing online players
        var presencePacket = LoginResponseBuilder.Presence(user.Id, user.Name, user.Privileges, 0);
        var statsPacket = LoginResponseBuilder.Stats(user.Id, null);
        _sessions.EnqueueToAllExcept(presencePacket, user.Id);
        _sessions.EnqueueToAllExcept(statsPacket, user.Id);

        var channels = await _db.ChatChannels.ToListAsync();
        foreach (var ch in channels)
        {
            _sessions.JoinChannel(ch.Name, session);
            var count = _sessions.GetChannelCount(ch.Name);
            var update = LoginResponseBuilder.ChannelInfo(ch.Name, ch.Description, count);
            _sessions.EnqueueToChannel(ch.Name, update);
        }
    }

    private async Task WriteFailure(HttpContext ctx)
    {
        ctx.Response.Headers["cho-token"] = "incorrect-credentials";
        ctx.Response.ContentType = "application/octet-stream";
        foreach (var packet in LoginResponseBuilder.Failure())
            await PacketSerializer.WritePacketAsync(ctx.Response.Body, packet);
    }
}
