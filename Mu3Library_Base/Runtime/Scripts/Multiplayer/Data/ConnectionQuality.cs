using System.Globalization;

namespace Mu3Library.Multiplayer.Data
{
    /// <summary>
    /// How this player's connection to another player performs, as the backend measured it over
    /// the last moment. A value is 0 until the backend has measured it, and how long that takes
    /// differs per backend: the local backend publishes its first measure half a second after the
    /// connection opens, and Steam reports the loss and the data rates once it has measured a few
    /// seconds of traffic (see the remarks of <c>SteamBackend.TryGetConnectionQuality</c>).
    /// </summary>
    public readonly struct ConnectionQuality
    {
        /// <summary>Round trip between the two players, in milliseconds.</summary>
        public int PingMilliseconds { get; }

        /// <summary>Share of the packets on their way to this player that the network lost, from 0 to 1.</summary>
        public float PacketLoss { get; }

        /// <summary>Bytes this player sent on the connection per second, over the last moment.</summary>
        public float BytesSentPerSecond { get; }

        /// <summary>Bytes that arrived on the connection per second, over the last moment.</summary>
        public float BytesReceivedPerSecond { get; }

        public ConnectionQuality(int pingMilliseconds, float packetLoss, float bytesSentPerSecond, float bytesReceivedPerSecond)
        {
            PingMilliseconds = pingMilliseconds;
            PacketLoss = packetLoss;
            BytesSentPerSecond = bytesSentPerSecond;
            BytesReceivedPerSecond = bytesReceivedPerSecond;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} ms, {1:0.#}% lost, {2:0} B/s out, {3:0} B/s in",
                PingMilliseconds, PacketLoss * 100f, BytesSentPerSecond, BytesReceivedPerSecond);
        }
    }
}
