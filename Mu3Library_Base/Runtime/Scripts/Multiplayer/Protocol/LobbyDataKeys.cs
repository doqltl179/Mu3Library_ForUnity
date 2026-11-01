using System;

namespace Mu3Library.Multiplayer.Protocol
{
    /// <summary>
    /// Lobby data keys the library writes for every lobby it creates. Steam does not tell lobby
    /// data keys apart by case, so a key is reserved whatever case its prefix is written in.
    /// </summary>
    internal static class LobbyDataKeys
    {
        public const string Prefix = "mu3.";

        public const string GameId = "mu3.game";
        public const string ProtocolVersion = "mu3.protocol";
        public const string WireVersion = "mu3.wire";
        public const string HostId = "mu3.host";
        public const string Name = "mu3.name";
        public const string State = "mu3.state";

        public const string StateLobby = "lobby";
        public const string StateInGame = "game";

        /// <summary>
        /// <see cref="ClosedForTheGame"/> from the start of a game that refuses late joiners to
        /// its end, <see cref="NotClosed"/> otherwise. The library's search keeps only lobbies that
        /// are not closed, so such a session stays out of it also while the host opens its lobby
        /// for a player coming back to a kept slot (FR-19, FR-20, FR-21), when Steam's own search
        /// would list it again.
        /// </summary>
        public const string Closed = "mu3.closed";
        public const string NotClosed = "0";
        public const string ClosedForTheGame = "1";

        public static bool IsReserved(string key) => key != null && key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
    }
}
