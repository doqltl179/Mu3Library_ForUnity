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
    }
}
