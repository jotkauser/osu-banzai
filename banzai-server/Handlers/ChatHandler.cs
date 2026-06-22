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

    public async Task HandleJoin(PlayerSession session, byte[] data)
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

    public async Task HandlePart(PlayerSession session, byte[] data)
    {
        var channelName = ReadChannelName(data);
        _sessions.LeaveChannel(channelName, session.UserId);

        var channel = await _db.ChatChannels.FirstOrDefaultAsync(c => c.Name == channelName);
        if (channel == null) return;

        var count = _sessions.GetChannelCount(channelName);
        var update = LoginResponseBuilder.ChannelInfo(channelName, channel.Description, count);
        _sessions.EnqueueToChannel(channelName, update);
    }

    private static string ReadChannelName(byte[] data)
    {
        var offset = 0;
        return PacketSerializer.ReadString(data, ref offset);
    }
}
