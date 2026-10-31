using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer.Local
{
    internal sealed class LocalConnection
    {
        public ConnectionId Id;
        public LocalBackend Owner;
        public PlayerId RemotePlayer;
        public LocalConnection Remote;
        public bool IsOpen;
        public ConnectionMeter Meter;

        /// <summary>
        /// When the last reliable packet, or the close, on its way to this connection arrives;
        /// the next one arrives no earlier, so they keep their order.
        /// </summary>
        public double LastOrderedDue;

        /// <summary>Packets on their way to this connection that the simulated network still holds back.</summary>
        public int DelayedCount;
    }
}
