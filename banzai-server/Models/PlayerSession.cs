using System.Collections.Concurrent;

namespace banzai_server.Models;

public class PlayerSession
{
    public string Token { get; }
    public long UserId { get; }
    public string Username { get; }
    public int Privileges { get; }
    public int UtcOffset { get; set; }
    public byte CountryCode { get; set; } = 0;
    public byte Mode { get; set; } = 0;
    public DateTime LastRequest { get; set; } = DateTime.UtcNow;

    public long? SpectatingUserId { get; set; }
    public ConcurrentDictionary<long, byte> SpectatorIds { get; } = new();

    // Current status from CHANGE_ACTION (packet 0)
    public byte Action { get; set; }
    public string InfoText { get; set; } = "";
    public string MapMd5 { get; set; } = "";
    public int Mods { get; set; }
    public int MapId { get; set; } = -1;

    private readonly ConcurrentQueue<BanchoPacket> _outbound = new();

    public PlayerSession(string token, long userId, string username, int privileges)
    {
        Token = token;
        UserId = userId;
        Username = username;
        Privileges = privileges;
    }

    public void Enqueue(BanchoPacket packet) => _outbound.Enqueue(packet);

    public List<BanchoPacket> DrainOutbound()
    {
        var packets = new List<BanchoPacket>();
        while (_outbound.TryDequeue(out var packet))
            packets.Add(packet);
        return packets;
    }
}
