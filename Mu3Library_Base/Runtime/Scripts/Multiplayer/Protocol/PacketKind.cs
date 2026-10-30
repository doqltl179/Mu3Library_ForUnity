namespace Mu3Library.Multiplayer.Protocol
{
    /// <summary>
    /// The first byte of every packet on a session connection. The values travel between players,
    /// so a value is never renumbered or reused.
    /// </summary>
    internal enum PacketKind : byte
    {
        /// <summary>Client to host: game identifier and protocol version.</summary>
        Hello = 1,
        /// <summary>Host to a new client: session state, rules, start data, and the roster.</summary>
        Welcome = 2,
        /// <summary>Host to clients: a player joined.</summary>
        ParticipantJoined = 3,
        /// <summary>Host to clients: a player left, with the reason.</summary>
        ParticipantLeft = 4,
        /// <summary>Host to clients: a player's ready or late-join flag changed.</summary>
        ParticipantUpdated = 5,
        /// <summary>Client to host: the sender's ready flag.</summary>
        SetReady = 6,
        /// <summary>Host to clients: the session rules changed.</summary>
        RulesChanged = 7,
        /// <summary>Host to clients: the game started, with the start data.</summary>
        GameStarted = 8,
        /// <summary>Host to clients: the game ended and the session is back in the lobby.</summary>
        ReturnedToLobby = 9,
    }
}
