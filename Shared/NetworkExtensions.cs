using System.Net.Sockets;

namespace Shared;

public static class NetworkExtensions
{
    public static async Task ReadExactAsync(this NetworkStream stream, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), ct);
            if (read == 0) throw new IOException("Connection closed unexpectedly");
            offset += read;
        }
    }
}
