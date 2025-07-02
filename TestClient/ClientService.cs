using Shared;
using System.Net;
using System.Net.Sockets;
using System.Text;
using static Shared.Enums;

namespace CliClient;

public static class ClientService
{
    public static async Task<bool> LoginAsync(NetworkStream stream, string username, string password, CancellationToken ct)
    {
        var creds = Encoding.UTF8.GetBytes($"{username}:{password}");
        var loginPkt = new Packet(Opcode.LoginRequest, creds);
        await stream.WriteAsync(loginPkt.ToBytes(), ct);

        var resp = await Packet.FromStream(stream, ct);
        return resp.Op == Opcode.LoginResponse && Encoding.UTF8.GetString(resp.Payload).StartsWith("OK");
    }

    public static async Task<WorldInfo[]> RequestRealmListAsync(NetworkStream stream, CancellationToken ct)
    {
        var req = new Packet(Opcode.RealmListRequest);
        await stream.WriteAsync(req.ToBytes(), ct);

        var resp = await Packet.FromStream(stream, ct);
        if (resp.Op != Opcode.RealmListResponse)
            return [];

        var worlds = new List<WorldInfo>();
        using var ms = new MemoryStream(resp.Payload);
        using var br = new BinaryReader(ms, Encoding.UTF8);

        while (ms.Position < ms.Length)
        {
            var id = br.ReadByte();
            var state = (WorldState)br.ReadByte();

            var ipLen = br.ReadByte();
            var ip = Encoding.UTF8.GetString(br.ReadBytes(ipLen));
            var port = IPAddress.NetworkToHostOrder(br.ReadInt16());

            var nameLen = br.ReadByte();
            var name = Encoding.UTF8.GetString(br.ReadBytes(nameLen));

            var currentUsers = IPAddress.NetworkToHostOrder(br.ReadInt16());
            var maxUsers = IPAddress.NetworkToHostOrder(br.ReadInt16());

            worlds.Add(new WorldInfo
            {
                Id = id,
                State = state,
                IP = IPAddress.Parse(ip),
                Port = port,
                Name = name,
                CurrentUsers = currentUsers,
                MaxUsers = maxUsers
            });
        }

        return [.. worlds];
    }

    public static async Task<TcpClient> ConnectWorldAsync(WorldInfo world, CancellationToken ct)
    {
        var tcp = new TcpClient();
        await tcp.ConnectAsync(world.IP.ToString(), world.Port, ct);
        var stream = tcp.GetStream();

        var hs = new Packet(Opcode.WorldHandshake);
        await stream.WriteAsync(hs.ToBytes(), ct);

        var resp = await Packet.FromStream(stream, ct);
        if (resp.Op == Opcode.WorldWelcome)
            Console.WriteLine(Encoding.UTF8.GetString(resp.Payload));

        return tcp;
    }

    public static async Task DisconnectWorldAsync(TcpClient tcpClient, CancellationToken ct)
    {
        var stream = tcpClient.GetStream();
        var discPkt = new Packet(Opcode.Disconnect);
        await stream.WriteAsync(discPkt.ToBytes(), ct);
        await Task.Delay(100, ct);
        tcpClient.Client.Shutdown(SocketShutdown.Both);
        tcpClient.Close();
    }

    public static async Task<bool> PingAsync(TcpClient tcpClient, CancellationToken ct)
    {
        var stream = tcpClient.GetStream();
        var pingPkt = new Packet(Opcode.Ping);
        await stream.WriteAsync(pingPkt.ToBytes(), ct);

        var resp = await Packet.FromStream(stream, ct);
        return resp.Op == Opcode.Pong;
    }

    public static async Task SetWorldStateAsync(TcpClient tcpClient, WorldState newState, CancellationToken ct)
    {
        var stream = tcpClient.GetStream();
        var setStatePkt = new Packet(Opcode.SetState, [(byte)newState]);
        await stream.WriteAsync(setStatePkt.ToBytes(), ct);
    }

    public static async Task ShutdownWorldAsync(TcpClient tcpClient, CancellationToken ct)
    {
        var stream = tcpClient.GetStream();
        var shutdownPkt = new Packet(Opcode.WorldShutdown);
        await stream.WriteAsync(shutdownPkt.ToBytes(), ct);
    }
}