using System.Globalization;

namespace Mu3Library.Multiplayer.Data
{
    /// <summary>
    /// How this player's connection to another player performs, as the backend measured it over
    /// the last moment. Every value is 0 until the backend has measured the connection, which
    /// takes about a second after it opens.
    /// </summary>
    public readonly struct ConnectionQuality
    {
        /// <summary>Round trip between the two players, in milliseconds.</summary>
        public int PingMilliseconds { get; }

        /// <summary>Share of the packets on their way to this player that the network lost, from 0 to 1.</summary>
        public float PacketLoss { get; }

        public float BytesSentPerSecond { get; }
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
