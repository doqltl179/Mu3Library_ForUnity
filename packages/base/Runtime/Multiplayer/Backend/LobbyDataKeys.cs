using System;

namespace Mu3Library.Multiplayer.Backend
{
    /// <summary>
    /// Lobby data the library reserves under <see cref="Prefix"/>, in any case, in every lobby a
    /// <see cref="MultiplayerManager"/> creates. The manager writes these keys, filters its lobby
    /// searches on them, leaves them out of the data a game reads, and refuses a game's data key
    /// with the prefix. A backend stores them like any other lobby data and may read them, as the
    /// Steam backend reads <see cref="State"/> to tell a lobby whose game runs from an invite-only
    /// lobby when Steam refuses entry with the same answer for both. Steam does not tell lobby data
    /// keys apart by case, so a key is reserved whatever case its prefix is written in.
    /// </summary>
    public static class LobbyDataKeys
    {
        /// <summary>The prefix of every reserved key, matched in any case (<see cref="IsReserved"/>).</summary>
        public const string Prefix = "mu3.";

        /// <summary>
        /// The game identifier, <see cref="MultiplayerConfig.GameId"/>. The manager's search keeps
        /// only lobbies whose value equals its own.
        /// </summary>
        public const string GameId = "mu3.game";

        /// <summary>
        /// The game's protocol version, <see cref="MultiplayerConfig.ProtocolVersion"/>, as a
        /// decimal number. The manager's search keeps only lobbies whose value equals its own.
        /// </summary>
        public const string ProtocolVersion = "mu3.protocol";

        /// <summary>
        /// The version of the library's packet format, as a decimal number. The manager's search
        /// keeps only lobbies whose value equals its own, so builds whose library writes another
        /// packet format do not meet.
        /// </summary>
        public const string WireVersion = "mu3.wire";

        /// <summary>
        /// The host's <see cref="PlayerId"/> value, as a decimal number. A client connects to this
        /// player after it enters the lobby, and the search reads it into
        /// <see cref="Data.LobbyInfo.HostId"/>.
        /// </summary>
        public const string HostId = "mu3.host";

        /// <summary>
        /// The lobby name, <see cref="Data.LobbyCreateOptions.Name"/>; absent when the name is
        /// empty. The search reads it into <see cref="Data.LobbyInfo.Name"/>.
        /// </summary>
        public const string Name = "mu3.name";

        /// <summary>
        /// <see cref="StateLobby"/> while the session is in the lobby and <see cref="StateInGame"/>
        /// from the start of a game to its end. The search reads it into
        /// <see cref="Data.LobbyInfo.IsInGame"/>.
        /// </summary>
        public const string State = "mu3.state";

        /// <summary>The value of <see cref="State"/> while the session is in the lobby.</summary>
        public const string StateLobby = "lobby";

        /// <summary>The value of <see cref="State"/> from the start of a game to its end.</summary>
        public const string StateInGame = "game";

        /// <summary>
        /// <see cref="ClosedForTheGame"/> from the start of a game that refuses late joiners to
        /// its end, <see cref="NotClosed"/> otherwise. The library's search keeps only lobbies
        /// whose value is <see cref="NotClosed"/>, so such a session stays out of it also while the
        /// host opens its lobby for a player coming back to a kept slot (FR-19, FR-20, FR-21), when
        /// Steam's own search would list it again. A lobby without the key is left out too: Steam's
        /// string filters drop a lobby that lacks the key under every comparison (AppID 480,
        /// 2026-11-01), so no filter keeps such lobbies on Steam, and dropping closed lobbies only
        /// after the search would spend the search's result slots on them. A player whose entry
        /// the lobby refused goes past it only when the key reads <see cref="ClosedForTheGame"/>,
        /// since Steam refuses an invite-only lobby of a game that takes late joiners the same way.
        /// </summary>
        public const string Closed = "mu3.closed";

        /// <summary>
        /// The value of <see cref="Closed"/> in the lobby and during a game that takes late joiners.
        /// </summary>
        public const string NotClosed = "0";

        /// <summary>The value of <see cref="Closed"/> during a game that refuses late joiners.</summary>
        public const string ClosedForTheGame = "1";

        /// <summary>
        /// True when <paramref name="key"/> starts with <see cref="Prefix"/> in any case; false
        /// for null.
        /// </summary>
        public static bool IsReserved(string key) => key != null && key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
    }
}
