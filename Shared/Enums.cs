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

        // World Operations
        Ping = 0x20,
        Pong = 0x21,
        SetState = 0x22,

        // General Error
        Error = 0xFF
    }

    public enum WorldState : byte
    {
        /// <summary>Server is offline/unreachable.</summary>
        Offline = 0,
        /// <summary>Server is up but not accepting new sessions.</summary>
        Closed = 1,
        /// <summary>Server is up and accepting new connections.</summary>
        Available = 2
    }
}
