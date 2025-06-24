using Shared;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using static Shared.Enums;

class Program
{
    private static WorldInfo _worldInfo;
    private static IPEndPoint _loginEndpoint;
    private static JsonSerializerOptions _jsonOptions;

    static async Task Main()
    {
        // Load settings
        var configText = await File.ReadAllTextAsync("worldsettings.json");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var settings = JsonSerializer.Deserialize<WorldSettings>(configText, options);
        if (settings == null)
        {
            Console.WriteLine("Failed to load worldsettings.json");
            return;
        }

        var hostIp = IPAddress.Parse(settings.Host);
        _worldInfo = new WorldInfo
        {
            Id = settings.Id,
            Name = settings.Name,
            IP = hostIp,
            Port = settings.Port,
            State = settings.State
        };

        _loginEndpoint = new IPEndPoint(
            IPAddress.Parse(settings.LoginServerHost),
            settings.LoginServerHeartbeatPort);

        _jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        Console.WriteLine($"Starting WorldServer '{_worldInfo.Name}' (ID={_worldInfo.Id}) on {settings.Host}:{settings.Port}, initial state={_worldInfo.State}");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        // Start heartbeats
        _ = HeartbeatLoop(settings.HeartbeatIntervalSeconds, cts.Token);

        // Start TCP listener for world clients
        var listener = new TcpListener(hostIp, settings.Port);
        listener.Start();
        Console.WriteLine("World server listening for clients...");

        while (!cts.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync(cts.Token);
                _ = HandleClientAsync(client, cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Console.WriteLine("Shutting down WorldServer.");
        listener.Stop();
    }

    private static async Task HeartbeatLoop(int intervalSeconds, CancellationToken ct)
    {
        using var udp = new UdpClient();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var json = JsonSerializer.Serialize(_worldInfo, _jsonOptions);
                var data = Encoding.UTF8.GetBytes(json);
                await udp.SendAsync(data, data.Length, _loginEndpoint);
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Heartbeat] Error: {ex.Message}");
            }
        }
    }

    private static async Task HandleClientAsync(TcpClient tcp, CancellationToken ct)
    {
        var sess = new Session(tcp);
        Console.WriteLine($"[World] Client connected: {sess.Id}");
        try
        {
            // Handshake
            var hs = await Packet.FromStream(sess.Stream, ct);
            if (hs.Op == Opcode.WorldHandshake)
            {
                var welcome = new Packet(Opcode.WorldWelcome, Encoding.UTF8.GetBytes($"Welcome to '{_worldInfo.Name}' WorldServer!"));
                await sess.Stream.WriteAsync(welcome.ToBytes(), ct);
            }

            // Main loop: Ping / Disconnect / SetState
            while (!ct.IsCancellationRequested)
            {
                var pkt = await Packet.FromStream(sess.Stream, ct);
                switch (pkt.Op)
                {
                    case Opcode.Ping:
                        var pong = new Packet(Opcode.Pong);
                        await sess.Stream.WriteAsync(pong.ToBytes(), ct);
                        break;

                    case Opcode.SetState:
                        if (pkt.Payload.Length >= 1)
                        {
                            var desired = (WorldState)pkt.Payload[0];
                            var previous = _worldInfo.State;
                            _worldInfo.State = desired;
                            Console.WriteLine($"[World] State changing from {previous} to {desired} by client.");
                            // notify login server of change
                            using var udp = new UdpClient();
                            var json = JsonSerializer.Serialize(_worldInfo, _jsonOptions);
                            var data = Encoding.UTF8.GetBytes(json);
                            await udp.SendAsync(data, data.Length, _loginEndpoint);
                        }
                        break;

                    case Opcode.Disconnect:
                        Console.WriteLine($"[World] Client {sess.Id} requested disconnect.");
                        return;

                    default:
                        Console.WriteLine($"[World] Unknown opcode {pkt.Op} from {sess.Id}");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[World] Error with client {sess.Id}: {ex.Message}");
        }
        finally
        {
            tcp.Close();
            Console.WriteLine($"[World] Client disconnected: {sess.Id}");
        }
    }
}