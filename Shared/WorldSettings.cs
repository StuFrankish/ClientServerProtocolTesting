using static Shared.Enums;

namespace Shared;
public class WorldSettings
{
    public byte Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 15001;
    public WorldState State { get; set; } = WorldState.Available;
    public string LoginServerHost { get; set; } = "127.0.0.1";
    public int LoginServerHeartbeatPort { get; set; } = 14004;
    public int HeartbeatIntervalSeconds { get; set; } = 5;
}