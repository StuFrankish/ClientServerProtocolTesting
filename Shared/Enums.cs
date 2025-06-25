namespace Shared;

public class Enums
{
    public enum Opcode : byte
    {
        // Login phase
        LoginRequest = 0x01,
        LoginResponse = 0x02,
        RealmListRequest = 0x03,
        RealmListResponse = 0x04,

        // World phase
        WorldHandshake = 0x10,
        WorldWelcome = 0x11,
        Disconnect = 0x12,

        // World Test Operations
        Ping = 0x20,
        Pong = 0x21,
        SetState = 0x22,
        GetHealth = 0x23,

        // General Error
        Error = 0xFF
    }

    public enum WorldState : byte
    {
        Offline = 0,
        Closed = 1,
        Available = 2
    }
}
