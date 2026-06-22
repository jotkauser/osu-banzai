using banzai_server.Models;
using banzai_server.Services;
using Microsoft.EntityFrameworkCore;

namespace banzai_server.Handlers;

public class ChatHandler
{
    private readonly BanzaiDbContext _db;
    private readonly SessionManager _sessions;

    public ChatHandler(BanzaiDbContext db, SessionManager sessions)
    {
        _db = db;
        _sessions = sessions;
    }

    public async Task HandlePublicMessage(PlayerSession session, byte[] data)
    {
        try
        {
            var (_, text, recipient, _) = PacketSerializer.ReadMessage(data);

            var trimmed = text.Trim();
            if (string.IsNullOrEmpty(trimmed)) return;

            if (trimmed.Length > 2000)
                trimmed = trimmed[..2000];

            var targetChannel = ResolveSpectatorChannel(session, recipient);
            var displayRecipient = targetChannel == recipient ? recipient : "#spectator";

            var channel = await _db.ChatChannels.FirstOrDefaultAsync(c => c.Name == targetChannel);

            _db.ChatMessages.Add(new ChatMessage
            {
                FromId = session.UserId,
                ChannelId = channel?.Id,
                Message = trimmed,
                CreatedAt = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync();

            var msgPacket = LoginResponseBuilder.SendMessage(session.Username, trimmed, displayRecipient, (int)session.UserId);
            _sessions.EnqueueToChannel(targetChannel, msgPacket, exceptUserId: session.UserId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[chat] {ex.Message}");
        }
    }

    public async Task HandleJoin(PlayerSession session, byte[] data)
    {
        var channelName = ReadChannelName(data);
        var resolved = ResolveSpectatorChannel(session, channelName);

        if (resolved != channelName)
        {
            // #spectator channel — join the real channel directly
            _sessions.JoinChannel(resolved, session);
            session.Enqueue(LoginResponseBuilder.ChannelJoinSuccess("#spectator"));

            var count = _sessions.GetChannelCount(resolved);
            var update = LoginResponseBuilder.ChannelInfo("#spectator", "Spectator channel", count);
            _sessions.EnqueueToChannel(resolved, update);
            return;
        }

        var channel = await _db.ChatChannels.FirstOrDefaultAsync(c => c.Name == channelName);
        if (channel == null) return;

        _sessions.JoinChannel(channelName, session);
        session.Enqueue(LoginResponseBuilder.ChannelJoinSuccess(channelName));

        var dbCount = _sessions.GetChannelCount(channelName);
        var dbUpdate = LoginResponseBuilder.ChannelInfo(channelName, channel.Description, dbCount);
        _sessions.EnqueueToChannel(channelName, dbUpdate);
    }

    public async Task HandlePart(PlayerSession session, byte[] data)
    {
        var channelName = ReadChannelName(data);
        var resolved = ResolveSpectatorChannel(session, channelName);

        if (resolved != channelName)
        {
            _sessions.LeaveChannel(resolved, session.UserId);
            var count = _sessions.GetChannelCount(resolved);
            var update = LoginResponseBuilder.ChannelInfo("#spectator", "Spectator channel", count);
            _sessions.EnqueueToChannel(resolved, update);
            return;
        }

        _sessions.LeaveChannel(channelName, session.UserId);

        var channel = await _db.ChatChannels.FirstOrDefaultAsync(c => c.Name == channelName);
        if (channel == null) return;

        var dbCount = _sessions.GetChannelCount(channelName);
        var dbUpdate = LoginResponseBuilder.ChannelInfo(channelName, channel.Description, dbCount);
        _sessions.EnqueueToChannel(channelName, dbUpdate);
    }

    private static string ReadChannelName(byte[] data)
    {
        var offset = 0;
        return PacketSerializer.ReadString(data, ref offset);
    }

    private static string ResolveSpectatorChannel(PlayerSession session, string channelName)
    {
        if (channelName != "#spectator") return channelName;

        if (session.SpectatingUserId.HasValue)
            return $"#spec_{session.SpectatingUserId.Value}";
        if (session.SpectatorIds.Count > 0)
            return $"#spec_{session.UserId}";

        return channelName;
    }
}
