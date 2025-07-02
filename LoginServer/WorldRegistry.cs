using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Shared;
using Shared.Caching;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using static Shared.Enums;

namespace LoginServer;

public class WorldRegistry(IConfiguration configuration)
{
    private readonly ConcurrentDictionary<byte, (WorldInfo Info, DateTime LastHeartbeat)> _worlds = new();
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(60);
    private readonly RedisWorldInfoCache _cache = new(connectionString: configuration.GetConnectionString("LoginServerRedis")!);

    public void StartHeartbeatListener(int port, CancellationToken ct)
    {
        var udp = new UdpClient(port);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // Receive loop
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await udp.ReceiveAsync(ct);
                    var json = Encoding.UTF8.GetString(result.Buffer);
                    var info = JsonSerializer.Deserialize<WorldInfo>(json, options);
                    if (info != null) Register(info);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Registry] Heartbeat error: {ex.Message}");
                }
            }
        }, ct);

        // Cleanup loop: expire stale entries
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                var now = DateTime.UtcNow;
                foreach (var key in _worlds.Keys.ToArray())
                {
                    if (_worlds.TryGetValue(key, out var entry))
                    {
                        if (entry.LastHeartbeat != DateTime.MinValue &&
                            now - entry.LastHeartbeat > _timeout)
                        {
                            var info = entry.Info;
                            info.State = WorldState.Offline;
                            _worlds[key] = (info, entry.LastHeartbeat);
                        }

                        // Update the cache with the latest info
                        await _cache.AddOrUpdateAsync(entry.Info).ConfigureAwait(false);
                    }
                }
            }
        }, ct);
    }

    public void Register(WorldInfo info)
    {
        _worlds.AddOrUpdate(
            info.Id,
            // Key doesn't exist - add new entry and log registration
            id =>
            {
                Console.WriteLine($"[Registry] World {info.Id} '{info.Name}' registered with state {info.State}");

                // Update the cache with the new world info
                _cache.AddOrUpdateAsync(info).ConfigureAwait(false);

                return (info, DateTime.UtcNow);
            },
            // Key exists - update only if state changed or if it's a heartbeat refresh
            (_, existing) =>
            {
                // Always update the timestamp for heartbeat tracking
                var timestamp = DateTime.UtcNow;

                // If state hasn't changed, preserve existing info but update timestamp
                if (existing.Info.State == info.State)
                {
                    return (existing.Info, timestamp);
                }

                // Update the cache with the new world info
                _cache.AddOrUpdateAsync(info).ConfigureAwait(false);

                // State changed - update info and timestamp
                Console.WriteLine($"[Registry] World {info.Id} '{info.Name}' state changed from {existing.Info.State} to {info.State}");
                return (info, timestamp);
            }
        );
    }

    public WorldInfo[] GetWorlds()
    {
        Console.WriteLine($"[Registry] Current worlds: {_worlds.Count} registered");
        return [.. _worlds.Values.Select(e => e.Info)];
    }
}

public class WorldRegistryHostedService : BackgroundService
{
    private readonly WorldRegistry _worldRegistry;

    public WorldRegistryHostedService(WorldRegistry worldRegistry)
    {
        _worldRegistry = worldRegistry;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _worldRegistry.StartHeartbeatListener(14004, stoppingToken);
        return Task.CompletedTask;
    }
}