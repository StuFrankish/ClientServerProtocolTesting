namespace Shared;

public class PlayerInfo
{
    public Guid SessionId { get; set; }
    public string UserId { get; set; }
    public (float X, float Y, float Z) Position { get; set; }
}