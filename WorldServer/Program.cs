using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using static Shared.Enums;

class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Load settings
                var configText = File.ReadAllText("worldsettings.json");
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var settings = JsonSerializer.Deserialize<WorldSettings>(configText, options);
                if (settings == null)
                {
                    throw new Exception("Failed to load worldsettings.json");
                }

                services.AddSingleton(settings);
                services.AddSingleton(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                services.AddSingleton(new WorldInfo
                {
                    Id = settings.Id,
                    Name = settings.Name,
                    IP = IPAddress.Parse(settings.Host),
                    Port = settings.Port,
                    State = settings.State
                });

                services.AddHostedService<HeartbeatService>();
                services.AddHostedService<WorldClientService>();
            })
            .Build();

        await host.RunAsync();
    }
}

public class HeartbeatService : IHostedService
{
    private readonly WorldSettings _settings;
    private readonly WorldInfo _worldInfo;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IPEndPoint _loginEndpoint;
    private CancellationTokenSource? _cts;

    public HeartbeatService(WorldSettings settings, WorldInfo worldInfo, JsonSerializerOptions jsonOptions)
    {
        _settings = settings;
        _worldInfo = worldInfo;
        _jsonOptions = jsonOptions;
        _loginEndpoint = new IPEndPoint(IPAddress.Parse(settings.LoginServerHost), settings.LoginServerHeartbeatPort);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = HeartbeatLoop(_settings.HeartbeatIntervalSeconds, _cts.Token);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    private async Task HeartbeatLoop(int intervalSeconds, CancellationToken ct)
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
}

public class WorldClientService : IHostedService
{
    private readonly WorldSettings _settings;
    private readonly WorldInfo _worldInfo;
    private readonly JsonSerializerOptions _jsonOptions;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public WorldClientService(WorldSettings settings, WorldInfo worldInfo, JsonSerializerOptions jsonOptions)
    {
        _settings = settings;
        _worldInfo = worldInfo;
        _jsonOptions = jsonOptions;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener = new TcpListener(IPAddress.Parse(_settings.Host), _settings.Port);
        _listener.Start();
        Console.WriteLine("World server listening for clients...");
        _ = AcceptClientsAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        _listener?.Stop();
        return Task.CompletedTask;
    }

    private async Task AcceptClientsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                _ = HandleClientAsync(client, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task HandleClientAsync(TcpClient tcp, CancellationToken ct)
    {
        var sess = new Session(tcp);
        Console.WriteLine($"[World] Client connected: {sess.Id}");
        try
        {
            var hs = await Packet.FromStream(sess.Stream, ct);
            if (hs.Op == Opcode.WorldHandshake)
            {
                var welcome = new Packet(Opcode.WorldWelcome, Encoding.UTF8.GetBytes($"Welcome to '{_worldInfo.Name}' WorldServer!"));
                await sess.Stream.WriteAsync(welcome.ToBytes(), ct);
            }

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

                            using var udp = new UdpClient();
                            var json = JsonSerializer.Serialize(_worldInfo, _jsonOptions);
                            var data = Encoding.UTF8.GetBytes(json);
                            await udp.SendAsync(data, data.Length, new IPEndPoint(IPAddress.Parse(_settings.LoginServerHost), _settings.LoginServerHeartbeatPort));
                        }
                        break;

                    case Opcode.Disconnect:
                        Console.WriteLine($"[World] Client {sess.Id} requested disconnect.");
                        return;

                    case Opcode.WorldShutdown:
                        Console.WriteLine($"[World] Client {sess.Id} requested server shutdown.");

                        _worldInfo.State = WorldState.Offline;

                        using (var udp = new UdpClient())
                        {
                            var json = JsonSerializer.Serialize(_worldInfo, _jsonOptions);
                            var data = Encoding.UTF8.GetBytes(json);
                            await udp.SendAsync(data, data.Length, new IPEndPoint(IPAddress.Parse(_settings.LoginServerHost), _settings.LoginServerHeartbeatPort));
                        }

                        Environment.Exit(0);
                        break;

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