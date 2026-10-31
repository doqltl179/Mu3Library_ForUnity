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

        public static bool IsReserved(string key) => key != null && key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
    }
}
