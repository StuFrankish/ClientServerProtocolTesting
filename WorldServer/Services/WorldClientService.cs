using Microsoft.Extensions.Hosting;
using Shared;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using static Shared.Enums;

public class WorldClientService : IHostedService
{
    private readonly WorldSettings _settings;
    private readonly WorldInfo _worldInfo;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ConcurrentDictionary<Guid, PlayerInfo> _connectedPlayers = new();
    private readonly ConcurrentBag<TcpClient> _connectedMonitors = new();
    private TcpListener? _clientListener;
    private TcpListener? _monitorListener;
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

        _clientListener = new TcpListener(IPAddress.Parse(_settings.Host), _settings.Port);
        _clientListener.Start();
        Console.WriteLine("World server listening for clients...");
        _ = AcceptClientsAsync(_cts.Token);

        _monitorListener = new TcpListener(IPAddress.Parse(_settings.Host), _settings.Port + 1);
        _monitorListener.Start();
        Console.WriteLine("World server listening for monitors...");
        _ = AcceptMonitorsAsync(_cts.Token);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        _clientListener?.Stop();
        _monitorListener?.Stop();
        return Task.CompletedTask;
    }

    private async Task AcceptClientsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _clientListener.AcceptTcpClientAsync(ct);
                _ = HandleClientAsync(client, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task AcceptMonitorsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var monitor = await _monitorListener.AcceptTcpClientAsync(ct);
                _connectedMonitors.Add(monitor);
                Console.WriteLine("[World] Monitor connected.");
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

        var playerInfo = new PlayerInfo
        {
            SessionId = sess.Id,
            UserId = "Unknown", // Placeholder until user ID is received
            Position = (0, 0, 0) // Default position
        };

        _connectedPlayers[sess.Id] = playerInfo;

        NotifyMonitors($"Client connected: {sess.Id}");

        try
        {
            var hs = await Packet.FromStream(sess.Stream, ct);
            if (hs.Op == Opcode.WorldHandshake && hs.Payload.Length > 0)
            {
                var username = Encoding.UTF8.GetString(hs.Payload);
                if (_connectedPlayers.TryGetValue(sess.Id, out var player))
                {
                    player.UserId = username;
                    Console.WriteLine($"[World] Client connected: {sess.Id}, Username: {username}");
                }

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

                    case Opcode.UpdatePlayerPosition:
                        if (pkt.Payload.Length >= 12) // Assuming position is sent as 3 floats (4 bytes each)
                        {
                            var x = BitConverter.ToSingle(pkt.Payload, 0);
                            var y = BitConverter.ToSingle(pkt.Payload, 4);
                            var z = BitConverter.ToSingle(pkt.Payload, 8);

                            if (_connectedPlayers.TryGetValue(sess.Id, out var player))
                            {
                                player.Position = (x, y, z);
                                Console.WriteLine($"[World] Player {player.UserId} position updated to ({x}, {y}, {z}).");
                            }
                        }
                        break;

                    case Opcode.QueryConnectedPlayers:
                        var playersJson = JsonSerializer.Serialize(_connectedPlayers.Values, _jsonOptions);
                        var responsePacket = new Packet(Opcode.QueryConnectedPlayersResponse, Encoding.UTF8.GetBytes(playersJson));
                        await sess.Stream.WriteAsync(responsePacket.ToBytes(), ct);
                        Console.WriteLine($"[World] Sent connected players list to client {sess.Id}.");
                        break;

                    case Opcode.Disconnect:
                        Console.WriteLine($"[World] Client {sess.Id} requested disconnect.");
                        _connectedPlayers.TryRemove(sess.Id, out _);
                        NotifyMonitors($"Client disconnected: {sess.Id}");
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
            _connectedPlayers.TryRemove(sess.Id, out _);
            NotifyMonitors($"Client disconnected: {sess.Id}");
            Console.WriteLine($"[World] Client disconnected: {sess.Id}");
        }
    }

    private void NotifyMonitors(string message)
    {
        var data = Encoding.UTF8.GetBytes(message);
        foreach (var monitor in _connectedMonitors)
        {
            try
            {
                monitor.GetStream().Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[World] Error notifying monitor: {ex.Message}");
            }
        }
    }
}