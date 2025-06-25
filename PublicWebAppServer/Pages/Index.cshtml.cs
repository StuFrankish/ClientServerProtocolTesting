using Microsoft.AspNetCore.Mvc.RazorPages;
using Shared;
using System.Net;
using System.Net.Sockets;
using System.Text;
using static Shared.Enums;

namespace PublicWebAppServer.Pages;

public class IndexModel(ILogger<IndexModel> logger) : PageModel
{
    public IList<WorldInfo> Realms { get; set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        TcpClient loginServerClient = default!;
        NetworkStream loginServerStream = default!;

        loginServerClient = new TcpClient();
        await loginServerClient.ConnectAsync(IPAddress.Parse("192.168.11.215"), 14002, cancellationToken);
        loginServerStream = loginServerClient.GetStream();

        await LoginAsync(loginServerStream, "alice", "alice", cancellationToken);

        Realms = await RequestRealmListAsync(loginServerStream, cancellationToken);
    }

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
}
