namespace Mu3Library.Multiplayer.Data
{
    public static class MultiplayerLimits
    {
        /// <summary>Smallest capacity a session accepts. A one-slot session plays alone on the same code path.</summary>
        public const int MinCapacity = 1;
        /// <summary>
        /// Largest capacity a session accepts. The host's machine runs the game and sends to every
        /// player, so the library supports and verifies sessions up to this size.
        /// </summary>
        public const int MaxCapacity = 16;
        public const int DefaultCapacity = 4;

        /// <summary>
        /// Largest start data, in bytes, that <see cref="IMultiplayerManager.StartGame"/> accepts.
        /// A late joiner receives it together with the whole roster in one packet, which has to
        /// stay well inside what one send may carry.
        /// </summary>
        public const int MaxStartDataSize = 64 * 1024;

        /// <summary>Longest player name, in characters, that the roster carries; longer names are cut.</summary>
        public const int MaxPlayerNameLength = 64;

        /// <summary>Longest key of lobby data or participant data, in characters.</summary>
        public const int MaxDataKeyLength = 64;

        /// <summary>
        /// Largest game-defined lobby data, counted as the UTF-8 bytes of all its keys and values.
        /// It stays well inside the 8 KB Steam takes for one lobby data value, and a joining player
        /// receives all of it with the roster in one packet.
        /// </summary>
        public const int MaxLobbyDataSize = 4 * 1024;

        /// <summary>
        /// Largest participant data of one player, counted as the UTF-8 bytes of all its keys and
        /// values. A joining player receives every player's with the roster in one packet, which
        /// has to stay inside one send even for <see cref="MaxCapacity"/> players.
        /// </summary>
        public const int MaxParticipantDataSize = 2 * 1024;

        /// <summary>
        /// Longest lobby chat message, in UTF-8 bytes: the 4 KB Steam allows for one lobby chat
        /// message, so a game that fits here fits there. It holds whatever
        /// <see cref="MultiplayerConfig.MaxMessageSize"/> is, which caps game messages only.
        /// </summary>
        public const int MaxChatMessageSize = 4 * 1024;

        /// <summary>
        /// Longest status text, in UTF-8 bytes, that <see cref="IMultiplayerManager.SetPresenceStatus"/>
        /// takes: the longest rich presence value Steam keeps, which takes 256 bytes with the NUL
        /// character that ends it.
        /// </summary>
        public const int MaxPresenceStatusSize = 255;

        /// <summary>
        /// Most ready changes, load reports, and participant data requests of one client that the
        /// host holds back for the rate limits (<see cref="SessionRules.MaxMessagesPerSecond"/>,
        /// <see cref="SessionRules.MaxBytesPerSecond"/>) at a time. One more disconnects the client
        /// with <see cref="MultiplayerReason.RateLimited"/>, even when
        /// <see cref="SessionRules.RateLimitKickSeconds"/> is 0, but without a ban, so it may join
        /// again. Each request holds no more than a data entry may, as the host refuses a larger
        /// one as it arrives.
        /// </summary>
        public const int MaxDelayedRequests = 64;

        /// <summary>
        /// Most connections of one account that the host keeps while they wait for its welcome,
        /// from the moment they open until the host admits the player or
        /// <see cref="MultiplayerConfig.HandshakeTimeoutSeconds"/> runs out. A player holds one
        /// such connection, and a returning player's next attempt may meet its last one for a
        /// moment. The host closes one more at once with <see cref="MultiplayerReason.Timeout"/>,
        /// as it closes a connection whose handshake timed out, so a returning player tries again.
        /// The backend names the account of each connection, and Steam's relays hide the remote
        /// address, so the limit counts accounts rather than addresses.
        /// </summary>
        public const int MaxPendingConnectionsPerAccount = 2;
    }
}
