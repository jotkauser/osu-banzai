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
