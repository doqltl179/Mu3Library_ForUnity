using System;

namespace Mu3Library.Multiplayer
{
    /// <summary>
    /// What a game tells the manager about itself. Players meet only when both values match, so
    /// raise <see cref="ProtocolVersion"/> whenever builds can no longer play together.
    /// </summary>
    public sealed class MultiplayerConfig
    {
        public const int MaxGameIdLength = 64;
        public const int DefaultMaxMessageSize = 64 * 1024;

        /// <summary>
        /// Tells this game's lobbies apart from other games on the same app ID, such as every
        /// game developed on Steam's test app 480. It holds 1 to <see cref="MaxGameIdLength"/>
        /// characters, and neither a NUL character nor an unpaired UTF-16 surrogate, or
        /// <see cref="MultiplayerManager.Initialize"/> answers
        /// <see cref="Data.MultiplayerReason.InvalidArgument"/>: the manager writes the ID into the
        /// lobby data and searches by it, and Steam ends the ID it holds at a NUL, as it did on
        /// AppID 480 on 2026-11-08, while every backend reads such a surrogate as U+FFFD, so the
        /// lobby would name another game than this one and search would leave out every lobby of
        /// this game.
        /// </summary>
        public string GameId { get; }
        public int ProtocolVersion { get; }

        /// <summary>How long joining (entering the lobby, connecting, and the host's answer) may take.</summary>
        public float JoinTimeoutSeconds { get; set; } = 15f;

        /// <summary>
        /// How long the host waits for a new connection to introduce itself and for its lobby to
        /// list the player as a member before closing it.
        /// </summary>
        public float HandshakeTimeoutSeconds { get; set; } = 10f;

        /// <summary>
        /// How long a client whose connection to its host broke during a game tries to come back
        /// to its slot, counted from when it noticed: 10 seconds by default, the time a backend
        /// takes to count a connection that stopped answering as lost. A client cannot tell its
        /// own network going down from the host's, so when this runs out before the host takes it
        /// back, the session ends with <see cref="Data.MultiplayerReason.HostLeft"/>, as for a
        /// host that stopped answering, rather than after the whole reservation time of the
        /// session's rules (<see cref="Data.SessionRules.ReconnectReservationSeconds"/>). The host
        /// keeps the slot for its reservation time all the same, and a client never tries longer
        /// than that. 0 ends the session as soon as the connection breaks.
        /// </summary>
        public float ReconnectTimeoutSeconds { get; set; } = 10f;

        /// <summary>
        /// Largest payload one game message carries, in bytes. It cannot exceed what one send of
        /// the backend carries after the message header, which is about 512 KiB on Steam.
        /// </summary>
        public int MaxMessageSize { get; set; } = DefaultMaxMessageSize;

        /// <summary>
        /// Seconds since an arbitrary start, for the timeouts. Null uses
        /// <c>Time.realtimeSinceStartupAsDouble</c>; tests hand in their own clock.
        /// </summary>
        public Func<double> Clock { get; set; }

        public MultiplayerConfig(string gameId, int protocolVersion)
        {
            GameId = gameId;
            ProtocolVersion = protocolVersion;
        }
    }
}
