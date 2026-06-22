using banzai_server.Models;
using Microsoft.EntityFrameworkCore;

namespace banzai_server.Services;

public static class LoginResponseBuilder
{
    // osu! client Permissions enum
    private const int Normal     = 1 << 0;
    private const int BAT        = 1 << 1;
    private const int Supporter  = 1 << 2;
    private const int Friend     = 1 << 3;
    private const int Peppy      = 1 << 4;
    private const int Tournament = 1 << 5;

    public static List<BanchoPacket> Failure()
    {
        return
        [
            I32(5, -1), // USER_ID = -1 = AUTHENTICATION_FAILED
        ];
    }

    public static async Task<List<BanchoPacket>> Build(User user, SessionManager sessions, BanzaiDbContext db)
    {
        var packets = new List<BanchoPacket>();
        var osuStats = user.UserStats.FirstOrDefault(s => s.Mode == 0);
        var privs = user.Privileges | Supporter; // always enable osu!direct

        // 1. PROTOCOL_VERSION (75): i32
        packets.Add(I32(75, 19));

        // 2. USER_ID (5): i32
        packets.Add(I32(5, (int)user.Id));

        // 3. PRIVILEGES (71): i32
        packets.Add(I32(71, privs));

        // 4. NOTIFICATION (24): string
        packets.Add(String(24, "Welcome back to osu!banzai!"));

        // 5. CHANNEL_INFO + CHANNEL_JOIN_SUCCESS
        var channels = await db.ChatChannels.ToListAsync();
        foreach (var ch in channels)
        {
            var count = sessions.GetChannelCount(ch.Name);
            packets.Add(ChannelInfo(ch.Name, ch.Description, count));
            packets.Add(ChannelJoinSuccess(ch.Name));
        }

        // 6. CHANNEL_INFO_END (89): no data
        packets.Add(new BanchoPacket(89, []));

        // 7. MAIN_MENU_ICON (76): string
        packets.Add(String(76, "|"));

        // 8. FRIENDS_LIST (72): empty
        packets.Add(List(72, []));

        // 9. SILENCE_END (92): i32 = 0
        packets.Add(I32(92, 0));

        // 10. USER_PRESENCE — self
        packets.Add(Presence(user.Id, user.Name, privs, 0));

        // 11. USER_STATS — self
        packets.Add(Stats(user.Id, osuStats));

        // 12. Other online players' presence + stats
        foreach (var other in sessions.OnlinePlayers)
        {
            if (other.UserId == user.Id) continue;
            packets.Add(Presence(other.UserId, other.Username, other.Privileges, other.UtcOffset));
            packets.Add(Stats(other.UserId, null));
        }

        // 13. SEND_MESSAGE (7) — TODO: offline mail

        // 14. ACCOUNT_RESTRICTED (104)
        if ((user.Privileges & Normal) == 0)
            packets.Add(new BanchoPacket(104, []));

        return packets;
    }

    public static BanchoPacket SendMessage(string sender, string text, string recipient, int senderId)
    {
        using var ms = new MemoryStream();
        PacketSerializer.WriteString(ms, sender);
        PacketSerializer.WriteString(ms, text);
        PacketSerializer.WriteString(ms, recipient);
        PacketSerializer.WriteI32(ms, senderId);
        return new BanchoPacket(7, ms.ToArray());
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

    public static BanchoPacket ChannelJoinSuccess(string name)
    {
        using var ms = new MemoryStream();
        PacketSerializer.WriteString(ms, name);
        return new BanchoPacket(64, ms.ToArray());
    }

    public static BanchoPacket ChannelInfo(string name, string topic, int playerCount)
    {
        using var ms = new MemoryStream();
        PacketSerializer.WriteString(ms, name);
        PacketSerializer.WriteString(ms, topic);
        PacketSerializer.WriteI32(ms, playerCount);
        return new BanchoPacket(65, ms.ToArray());
    }

    private static BanchoPacket Presence(long userId, string username, int privileges, int utcOffset)
    {
        using var ms = new MemoryStream();
        PacketSerializer.WriteI32(ms, (int)userId);
        PacketSerializer.WriteString(ms, username);
        PacketSerializer.WriteU8(ms, (byte)(utcOffset + 24));
        PacketSerializer.WriteU8(ms, 0); // country code
        PacketSerializer.WriteU8(ms, (byte)((privileges & 0x1F) | (0 << 5))); // priv bottom 5 bits | mode << 5
        PacketSerializer.WriteF32(ms, 0.0f); // longitude
        PacketSerializer.WriteF32(ms, 0.0f); // latitude
        PacketSerializer.WriteI32(ms, 0); // rank
        return new BanchoPacket(83, ms.ToArray());
    }

    public static BanchoPacket Stats(long userId, UserStat? stats)
    {
        using var ms = new MemoryStream();
        PacketSerializer.WriteI32(ms, (int)userId);
        PacketSerializer.WriteU8(ms, 0);  // action = idle
        PacketSerializer.WriteString(ms, "");
        PacketSerializer.WriteString(ms, "");
        PacketSerializer.WriteI32(ms, 0);  // mods
        PacketSerializer.WriteU8(ms, 0);  // mode = osu!
        PacketSerializer.WriteI32(ms, -1); // map id
        PacketSerializer.WriteI64(ms, stats?.RankedScore ?? 0);
        PacketSerializer.WriteF32(ms, stats != null ? (float)(stats.Accuracy / 100.0) : 0.0f);
        PacketSerializer.WriteI32(ms, stats?.Playcount ?? 0);
        PacketSerializer.WriteI64(ms, stats?.TotalScore ?? 0);
        PacketSerializer.WriteI32(ms, 0);  // rank
        PacketSerializer.WriteU16(ms, (ushort)(stats?.Pp ?? 0));
        return new BanchoPacket(11, ms.ToArray());
    }
}
