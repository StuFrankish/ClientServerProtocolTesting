using System.Net.Sockets;
using static Shared.Enums;

namespace Shared;

public class Packet(Opcode op, byte[]? payload = null)
{
    public Opcode Op { get; } = op;
    public byte[] Payload { get; } = payload ?? [];

    public byte[] ToBytes()
    {
        ushort len = (ushort)(1 + Payload.Length);
        var buf = new byte[2 + len];

        // network byte order
        buf[0] = (byte)(len >> 8);
        buf[1] = (byte)(len & 0xFF);
        buf[2] = (byte)Op;
        Buffer.BlockCopy(Payload, 0, buf, 3, Payload.Length);
        return buf;
    }

    public static async Task<Packet> FromStream(NetworkStream stream, CancellationToken ct)
    {
        // read length
        var header = new byte[2];
        await stream.ReadExactAsync(header, ct);
        var len = (ushort)((header[0] << 8) | header[1]);

        // read body
        var body = new byte[len];
        await stream.ReadExactAsync(body, ct);

        var op = (Opcode)body[0];
        var payload = new byte[len - 1];
        Buffer.BlockCopy(body, 1, payload, 0, payload.Length);
        return new Packet(op, payload);
    }
}