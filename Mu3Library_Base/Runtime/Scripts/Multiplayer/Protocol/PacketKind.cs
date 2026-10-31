namespace Mu3Library.Multiplayer.Protocol
{
    /// <summary>
    /// The first byte of every packet on a session connection. The values travel between players,
    /// so a value is never renumbered or reused.
    /// </summary>
    internal enum PacketKind : byte
    {
        /// <summary>
        /// Client to host: wire version, game identifier, game protocol version, and whether the
        /// client comes back to the slot the host keeps for it.
        /// </summary>
        Hello = 1,
        /// <summary>Host to a new or returning client: session state, game number, rules, message size limit, start data, lobby data, and the roster.</summary>
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
        /// <summary>Host to clients: the game started, with its number and the start data.</summary>
        GameStarted = 8,
        /// <summary>Host to clients: the game ended and the session is back in the lobby.</summary>
        ReturnedToLobby = 9,
        /// <summary>Client to host: a game message with its delivery mode and target.</summary>
        MessageToHost = 10,
        /// <summary>Host to a client: a game message with its delivery mode and its sender.</summary>
        MessageFromHost = 11,
        /// <summary>Host to clients: one value of the game's lobby data changed.</summary>
        LobbyDataChanged = 12,
        /// <summary>Client to host: one value of the sender's own participant data.</summary>
        SetParticipantData = 13,
        /// <summary>Host to clients: one value of a player's participant data changed.</summary>
        ParticipantDataChanged = 14,
        /// <summary>Client to host: a lobby chat message.</summary>
        ChatToHost = 15,
        /// <summary>Host to clients: a lobby chat message with its sender.</summary>
        ChatFromHost = 16,
        /// <summary>Host to clients: a player's connection dropped, and the host keeps their slot.</summary>
        ParticipantDisconnected = 17,
        /// <summary>Host to clients: a player whose slot the host kept came back to it.</summary>
        ParticipantReconnected = 18,
    }
}
