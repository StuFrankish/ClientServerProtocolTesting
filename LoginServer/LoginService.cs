using Microsoft.EntityFrameworkCore;
using Shared;
using Shared.Persistence;
using System.Net;
using System.Net.Sockets;
using System.Text;
using static Shared.Enums;

namespace LoginServer;

public class LoginService(WorldRegistry registry, LoginDbContext dbContext)
{
    private readonly TcpListener _listener = new(GetLocalIPAddress(), 14002);
    private readonly WorldRegistry _registry = registry;
    private readonly LoginDbContext _loginDbContext = dbContext;

    private static IPAddress GetLocalIPAddress()
    {
        return IPAddress.Parse("192.168.11.215");
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _listener.Start();
        Console.WriteLine($"[Login] Listening on {_listener.LocalEndpoint}");

        while (!ct.IsCancellationRequested)
        {
            var tcp = await _listener.AcceptTcpClientAsync(ct);
            _ = HandleClientAsync(new Session(tcp), ct);
        }
    }

    private async Task HandleClientAsync(Session sess, CancellationToken ct)
    {
        Console.WriteLine($"[Login] Connection {sess.Id}");
        try
        {
            // 1) Authenticate
            var pkt = await Packet.FromStream(sess.Stream, ct);
            if (pkt.Op != Opcode.LoginRequest) return;

            var creds = Encoding.UTF8.GetString(pkt.Payload).Split(':', 2);
            var user = creds.Length > 0 ? creds[0] : string.Empty;
            var pass = creds.Length > 1 ? creds[1] : string.Empty;

            var internalUser = await _loginDbContext.Users.FirstOrDefaultAsync(u => u.Username == user && u.Password == pass, ct);
            var text = internalUser != null ? $"OK" : "FAIL";

            Console.WriteLine($"[Login] User `{user}` {(internalUser != null ? "authenticated" : "failed")}");

            var resp = new Packet(Opcode.LoginResponse, Encoding.UTF8.GetBytes(text));
            await sess.Stream.WriteAsync(resp.ToBytes(), ct);
            if (internalUser is null) return;

            // 2) Send realm list upon request
            var listReq = await Packet.FromStream(sess.Stream, ct);
            if (listReq.Op != Opcode.RealmListRequest) return;

            var worlds = _registry.GetWorlds();
            using var ms = new MemoryStream();
            foreach (var w in worlds)
            {
                // ID and state
                ms.WriteByte(w.Id);
                ms.WriteByte((byte)w.State);

                // IP address
                var ipBytes = Encoding.UTF8.GetBytes(w.IP.ToString());
                ms.WriteByte((byte)ipBytes.Length);
                ms.Write(ipBytes);

                // port (network‐byte order)
                var portBytes = BitConverter.GetBytes((ushort)IPAddress.HostToNetworkOrder((short)w.Port));
                ms.Write(portBytes, 0, portBytes.Length);

                // realm name
                var nmBytes = Encoding.UTF8.GetBytes(w.Name);
                ms.WriteByte((byte)nmBytes.Length);
                ms.Write(nmBytes, 0, nmBytes.Length);

                // current user count (Int16, network‐byte order)
                var cuBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)w.CurrentUsers));
                ms.Write(cuBytes, 0, cuBytes.Length);

                // maximum user count (Int16, network‐byte order)
                var muBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)w.MaxUsers));
                ms.Write(muBytes, 0, muBytes.Length);
            }

            var listResp = new Packet(Opcode.RealmListResponse, ms.ToArray());
            await sess.Stream.WriteAsync(listResp.ToBytes(), ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Login] Error {sess.Id}: {ex.Message}");
        }
        finally
        {
            Console.WriteLine($"[Login] Disconnected {sess.Id}");
            sess.Client.Close();
        }
    }
}