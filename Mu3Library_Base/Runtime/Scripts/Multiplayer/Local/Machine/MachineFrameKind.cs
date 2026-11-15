namespace Mu3Library.Multiplayer.Local.Machine
{
    /// <summary>
    /// What one frame between two processes of a machine-wide local network carries.
    /// </summary>
    internal enum MachineFrameKind : byte
    {
        /// <summary>
        /// It keeps a quiet connection from looking stalled. Empty, or on a session connection a
        /// quality probe (<see cref="MachineProtocol.ProbeRequest"/>), which a version that does
        /// not measure ignores.
        /// </summary>
        Ping = 1,
        /// <summary>The sender closes the connection, with the reason code it passed.</summary>
        Close = 2,
        /// <summary>A packet of the session.</summary>
        Data = 3,

        /// <summary>First frame of a session connection: who connects, and to whom.</summary>
        SessionHello = 10,
        SessionAccept = 11,
        /// <summary>The player is not listening, or is not the one the connection asked for.</summary>
        SessionRefuse = 12,

        /// <summary>First frame of a lobby connection: a player asks the owner to enter a lobby.</summary>
        LobbyJoin = 20,
        /// <summary>The owner's answer, with the lobby when the player entered it.</summary>
        LobbyReply = 21,
        /// <summary>The lobby after a change, from the owner to each member.</summary>
        LobbyState = 22,
        /// <summary>The owner leaves and names the member that takes the lobby over.</summary>
        LobbyHandover = 23,
    }
}
