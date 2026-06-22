using banzai_server.Models;

namespace banzai_server.Services;

public static class BanchoPackets
{
    public static BanchoPacket SendMessage(string sender, string text, string recipient, int senderId)
    {
        using var ms = new MemoryStream();
        PacketSerializer.WriteString(ms, sender);
        PacketSerializer.WriteString(ms, text);
        PacketSerializer.WriteString(ms, recipient);
        PacketSerializer.WriteI32(ms, senderId);
        return new BanchoPacket(7, ms.ToArray());
    }

    public static BanchoPacket ChannelKick(string name)
    {
        using var ms = new MemoryStream();
        PacketSerializer.WriteString(ms, name);
        return new BanchoPacket(66, ms.ToArray());
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

    public static BanchoPacket SpectatorJoined(int userId) => I32(13, userId);
    public static BanchoPacket SpectatorLeft(int userId) => I32(14, userId);
    public static BanchoPacket SpectateFrames(byte[] rawData) => new(15, rawData);
    public static BanchoPacket SpectatorCantSpectate(int userId) => I32(22, userId);
    public static BanchoPacket FellowSpectatorJoined(int userId) => I32(42, userId);
    public static BanchoPacket FellowSpectatorLeft(int userId) => I32(43, userId);

    public static BanchoPacket Presence(long userId, string username, int privileges, int utcOffset)
    {
        using var ms = new MemoryStream();
        PacketSerializer.WriteI32(ms, (int)userId);
        PacketSerializer.WriteString(ms, username);
        PacketSerializer.WriteU8(ms, (byte)(utcOffset + 24));
        PacketSerializer.WriteU8(ms, 0); // country code
        PacketSerializer.WriteU8(ms, (byte)((privileges & 0x1F) | (0 << 5)));
        PacketSerializer.WriteF32(ms, 0.0f); // longitude
        PacketSerializer.WriteF32(ms, 0.0f); // latitude
        PacketSerializer.WriteI32(ms, 0); // rank
        return new BanchoPacket(83, ms.ToArray());
    }

    public static BanchoPacket Stats(long userId, UserStat? stats)
    {
        return BuildStats(userId, 0, "", "", 0, 0, -1, stats);
    }

    public static BanchoPacket Stats(PlayerSession session)
    {
        return BuildStats(session.UserId, session.Action, session.InfoText, session.MapMd5,
            session.Mods, session.Mode, session.MapId, null);
    }

    private static BanchoPacket BuildStats(long userId, byte action, string infoText, string mapMd5,
        int mods, byte mode, int mapId, UserStat? stats)
    {
        using var ms = new MemoryStream();
        PacketSerializer.WriteI32(ms, (int)userId);
        PacketSerializer.WriteU8(ms, action);
        PacketSerializer.WriteString(ms, infoText);
        PacketSerializer.WriteString(ms, mapMd5);
        PacketSerializer.WriteI32(ms, mods);
        PacketSerializer.WriteU8(ms, mode);
        PacketSerializer.WriteI32(ms, mapId);
        PacketSerializer.WriteI64(ms, stats?.RankedScore ?? 0);
        PacketSerializer.WriteF32(ms, stats != null ? (float)(stats.Accuracy / 100.0) : 0.0f);
        PacketSerializer.WriteI32(ms, stats?.Playcount ?? 0);
        PacketSerializer.WriteI64(ms, stats?.TotalScore ?? 0);
        PacketSerializer.WriteI32(ms, 0);  // rank
        PacketSerializer.WriteU16(ms, (ushort)(stats?.Pp ?? 0));
        return new BanchoPacket(11, ms.ToArray());
    }

    private static BanchoPacket I32(ushort id, int value)
    {
        using var ms = new MemoryStream();
        PacketSerializer.WriteI32(ms, value);
        return new BanchoPacket(id, ms.ToArray());
    }
}
