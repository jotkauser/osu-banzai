using System.Text;
using banzai_server.Models;

namespace banzai_server.Services;

public static class PacketSerializer
{
    // Wire format: [u16 id, LE] [u8 padding=0] [u32 data_len, LE] [data: bytes]
    // Multiple packets concatenated.

    public static List<BanchoPacket> ReadPackets(Stream stream)
    {
        var packets = new List<BanchoPacket>();

        while (stream.Position < stream.Length)
        {
            var header = new byte[7];
            var read = stream.Read(header, 0, 7);
            if (read < 7) break;

            var id = BitConverter.ToUInt16(header, 0);
            var len = BitConverter.ToUInt32(header, 3);

            var data = new byte[len];
            if (len > 0)
            {
                var dataRead = stream.Read(data, 0, (int)len);
                if (dataRead < (int)len) break;
            }

            packets.Add(new BanchoPacket(id, data));
        }

        return packets;
    }

    public static async Task WritePacketAsync(Stream stream, BanchoPacket packet)
    {
        var header = new byte[7];
        BitConverter.GetBytes(packet.Id).CopyTo(header, 0);
        BitConverter.GetBytes((uint)packet.Data.Length).CopyTo(header, 3);

        await stream.WriteAsync(header, 0, 7);
        if (packet.Data.Length > 0)
            await stream.WriteAsync(packet.Data, 0, packet.Data.Length);
    }

    public static void WriteI32(MemoryStream ms, int value)
    {
        var bytes = BitConverter.GetBytes(value);
        ms.Write(bytes, 0, 4);
    }

    public static void WriteU8(MemoryStream ms, byte value)
    {
        ms.WriteByte(value);
    }

    public static void WriteF32(MemoryStream ms, float value)
    {
        var bytes = BitConverter.GetBytes(value);
        ms.Write(bytes, 0, 4);
    }

    public static void WriteI64(MemoryStream ms, long value)
    {
        var bytes = BitConverter.GetBytes(value);
        ms.Write(bytes, 0, 8);
    }

    public static void WriteU16(MemoryStream ms, ushort value)
    {
        var bytes = BitConverter.GetBytes(value);
        ms.Write(bytes, 0, 2);
    }

    // osu! string: u8(0x0B) + ULEB128 length + UTF-8 bytes
    public static void WriteString(MemoryStream ms, string value)
    {
        ms.WriteByte(0x0B);
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteUleb128(ms, (uint)bytes.Length);
        ms.Write(bytes, 0, bytes.Length);
    }

    // i32_list with i16 length prefix
    public static void WriteI32List(MemoryStream ms, int[] ids)
    {
        WriteU16(ms, (ushort)ids.Length);
        foreach (var id in ids)
            WriteI32(ms, id);
    }

    public static byte ReadU8(byte[] data, ref int offset)
    {
        return data[offset++];
    }

    public static int ReadI32(byte[] data, ref int offset)
    {
        var value = BitConverter.ToInt32(data, offset);
        offset += 4;
        return value;
    }

    public static string ReadString(byte[] data, ref int offset)
    {
        var presence = data[offset++]; // 0x0B
        if (presence != 0x0B)
            throw new InvalidDataException($"Expected string presence byte 0x0B, got 0x{presence:X2}");

        var len = ReadUleb128(data, ref offset);
        if (len == 0) return "";

        var str = Encoding.UTF8.GetString(data, offset, (int)len);
        offset += (int)len;
        return str;
    }

    // message: string sender, string text, string recipient, i32 sender_id
    public static (string sender, string text, string recipient, int senderId) ReadMessage(byte[] data)
    {
        var offset = 0;
        var sender = ReadString(data, ref offset);
        var text = ReadString(data, ref offset);
        var recipient = ReadString(data, ref offset);
        var senderId = ReadI32(data, ref offset);
        return (sender, text, recipient, senderId);
    }

    private static uint ReadUleb128(byte[] data, ref int offset)
    {
        uint value = 0;
        int shift = 0;
        while (true)
        {
            var b = data[offset++];
            value |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
        }
        return value;
    }

    private static void WriteUleb128(MemoryStream ms, uint value)
    {
        while (value >= 0x80)
        {
            ms.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }
        ms.WriteByte((byte)value);
    }
}
