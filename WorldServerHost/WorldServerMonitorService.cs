using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Shared;
using static Shared.Enums;

public class WorldServerMonitorService(string serverHost, int serverPort) : IDisposable
{
    private readonly string _serverHost = serverHost;
    private readonly int _serverPort = serverPort;
    private TcpClient? _client;

    public event Action<string>? OnMessageReceived;

    public async Task ConnectAsync()
    {
        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(_serverHost, _serverPort);
            Console.WriteLine("Connected to WorldServer.");
            _ = ListenForUpdatesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to connect to WorldServer: {ex.Message}");
        }
    }

    private async Task ListenForUpdatesAsync()
    {
        if (_client?.GetStream() is NetworkStream stream)
        {
            var buffer = new byte[1024];
            while (_client.Connected)
            {
                try
                {
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        OnMessageReceived?.Invoke(message);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading from WorldServer: {ex.Message}");
                    break;
                }
            }
        }
    }

    public void Disconnect()
    {
        _client?.Close();
        _client = null;

        Console.WriteLine("Disconnected from WorldServer.");
    }

    public void Dispose()
    {
        try
        {
            Disconnect();
            _client?.Dispose();
            _client = null;

            Console.WriteLine("WorldServerMonitorService disposed.");
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<List<PlayerInfo>> QueryActivePlayersAsync(CancellationToken ct = default)
    {
        var stream = _client.GetStream();
        var queryPkt = new Packet(Opcode.QueryConnectedPlayers);
        await stream.WriteAsync(queryPkt.ToBytes(), ct);

        var resp = await Packet.FromStream(stream, ct);
        if (resp.Op == Opcode.QueryConnectedPlayersResponse)
        {
            // Setup JSON deserialization options to ignore case differences
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };

            var playersJson = Encoding.UTF8.GetString(resp.Payload);
            return JsonSerializer.Deserialize<List<PlayerInfo>>(playersJson, options) ?? [];
        }

        return [];
    }
}