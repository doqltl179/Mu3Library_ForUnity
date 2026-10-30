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
    }
}
