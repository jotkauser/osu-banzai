using banzai_server.Models;
using Microsoft.EntityFrameworkCore;

namespace banzai_server.Services;

public static class LoginResponseBuilder
{
    private const int Normal     = 1 << 0;
    private const int Supporter  = 1 << 2;

    public static List<BanchoPacket> Failure()
    {
        return [I32(5, -1)];
    }

    public static async Task<List<BanchoPacket>> Build(User user, SessionManager sessions, BanzaiDbContext db)
    {
        var packets = new List<BanchoPacket>();
        var osuStats = user.UserStats.FirstOrDefault(s => s.Mode == 0);
        var privs = user.Privileges | Supporter;

        packets.Add(I32(75, 19)); // PROTOCOL_VERSION
        packets.Add(I32(5, (int)user.Id)); // USER_ID
        packets.Add(I32(71, privs)); // PRIVILEGES

        var channels = await db.ChatChannels.ToListAsync();
        foreach (var ch in channels)
        {
            var count = sessions.GetChannelCount(ch.Name);
            packets.Add(BanchoPackets.ChannelInfo(ch.Name, ch.Description, count));
            packets.Add(BanchoPackets.ChannelJoinSuccess(ch.Name));
        }

        packets.Add(new BanchoPacket(89, [])); // CHANNEL_INFO_END
        packets.Add(String(76, "|")); // MAIN_MENU_ICON

        var friendIds = await db.UserFriends
            .Where(f => f.UserId == user.Id)
            .Select(f => (int)f.FriendId)
            .ToArrayAsync();
        packets.Add(List(72, friendIds)); // FRIENDS_LIST

        packets.Add(I32(92, 0)); // SILENCE_END

        packets.Add(BanchoPackets.Presence(user.Id, user.Name, privs, 0));
        packets.Add(BanchoPackets.Stats(user.Id, osuStats));

        foreach (var other in sessions.OnlinePlayers)
        {
            if (other.UserId == user.Id) continue;
            packets.Add(BanchoPackets.Presence(other.UserId, other.Username, other.Privileges, other.UtcOffset));
            packets.Add(BanchoPackets.Stats(other));
        }

        if ((user.Privileges & Normal) == 0)
            packets.Add(new BanchoPacket(104, [])); // ACCOUNT_RESTRICTED

        return packets;
    }

    public static BanchoPacket Restart(int delayMs = 0)
    {
        using var ms = new MemoryStream();
        PacketSerializer.WriteI32(ms, delayMs);
        return new BanchoPacket(86, ms.ToArray());
    }

    public static BanchoPacket Logout(long userId)
    {
        using var ms = new MemoryStream();
        PacketSerializer.WriteI32(ms, (int)userId);
        PacketSerializer.WriteU8(ms, 0);
        return new BanchoPacket(12, ms.ToArray());
    }

    private static BanchoPacket I32(ushort id, int value)
    {
        using var ms = new MemoryStream();
        PacketSerializer.WriteI32(ms, value);
        return new BanchoPacket(id, ms.ToArray());
    }

    private static BanchoPacket String(ushort id, string value)
    {
        using var ms = new MemoryStream();
        PacketSerializer.WriteString(ms, value);
        return new BanchoPacket(id, ms.ToArray());
    }

    private static BanchoPacket List(ushort id, int[] ids)
    {
        using var ms = new MemoryStream();
        PacketSerializer.WriteI32List(ms, ids);
        return new BanchoPacket(id, ms.ToArray());
    }
}
