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
        /// game developed on Steam's test app 480.
        /// </summary>
        public string GameId { get; }
        public int ProtocolVersion { get; }

        /// <summary>How long joining (entering the lobby, connecting, and the host's answer) may take.</summary>
        public float JoinTimeoutSeconds { get; set; } = 15f;

        /// <summary>How long the host waits for a new connection to introduce itself before closing it.</summary>
        public float HandshakeTimeoutSeconds { get; set; } = 10f;

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
