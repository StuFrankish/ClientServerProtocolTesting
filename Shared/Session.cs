using System.Net.Sockets;

namespace Shared;
public class Session(TcpClient client)
{
    public Guid Id { get; } = Guid.NewGuid();
    public TcpClient Client { get; } = client;
    public NetworkStream Stream => Client.GetStream();
}
