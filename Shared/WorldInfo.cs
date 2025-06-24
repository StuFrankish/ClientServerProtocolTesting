using System.Net;
using System.Text.Json.Serialization;
using static Shared.Enums;

namespace Shared;

public class WorldInfo
{
    public byte Id { get; init; }
    public string Name { get; init; } = string.Empty;

    [JsonConverter(typeof(IPAddressJsonConverter))]
    public IPAddress IP { get; init; } = IPAddress.Loopback;

    public int Port { get; init; }
    public WorldState State { get; set; }

    public int MaxUsers { get; init; } = 10;
    public int CurrentUsers { get; set; } = 0;

    public int Capacity => MaxUsers - CurrentUsers;
}