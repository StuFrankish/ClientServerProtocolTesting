using Microsoft.Extensions.Hosting;
using Shared;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

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
