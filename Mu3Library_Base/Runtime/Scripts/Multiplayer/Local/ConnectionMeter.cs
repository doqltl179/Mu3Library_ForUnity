using System;
using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer.Local
{
    /// <summary>
    /// Measures one session connection of a local backend: the round trip of the probes that
    /// cross it, the share of packets on their way here that the simulated network lost, and the
    /// bytes each way. It publishes them every <see cref="WindowSeconds"/>.
    /// </summary>
    /// <remarks>
    /// A probe's round trip is the delay the simulated network gave it on the way out and on the
    /// way back. The time a packet then waits for the next <c>Update</c> of a backend is left
    /// out, as Steam's ping leaves out how often a game reads its messages; without simulated
    /// latency the ping is therefore 0.
    /// </remarks>
    internal sealed class ConnectionMeter
    {
        public const double ProbeIntervalSeconds = 0.25;
        public const double WindowSeconds = 0.5;

        // Each window's packet counts weigh this much less in the next window, so the loss covers
        // the last few windows: a window holds too few probes to measure loss on its own.
        private const double LossDecay = 0.75;

        private double _windowStart;
        private double _nextProbe;

        private long _bytesSent;
        private long _bytesReceived;
        private double _roundTripSeconds;
        private int _roundTrips;
        private double _packetsIn;
        private double _packetsLostIn;

        private ConnectionQuality _quality;
        public ConnectionQuality Quality => _quality;



        public ConnectionMeter(double now)
        {
            _windowStart = now;
            _nextProbe = now;
        }

        public void AddSent(int bytes) => _bytesSent += bytes;
        public void AddReceived(int bytes) => _bytesReceived += bytes;

        /// <summary>Packets the other side sent toward this one, and how many of them the network lost.</summary>
        public void AddInbound(long packets, long lost)
        {
            _packetsIn += packets;
            _packetsLostIn += lost;
        }

        public void AddRoundTrip(double seconds)
        {
            _roundTripSeconds += seconds;
            _roundTrips++;
        }

        /// <summary>True when the connection should send its next probe now.</summary>
        public bool TakeProbe(double now)
        {
            if (now < _nextProbe)
            {
                return false;
            }

            _nextProbe = now + ProbeIntervalSeconds;
            return true;
        }

        /// <summary>Publishes what the window measured once it is over, and starts the next one.</summary>
        public void Update(double now)
        {
            double elapsed = now - _windowStart;
            if (elapsed < WindowSeconds)
            {
                return;
            }

            // A window without a probe that came back keeps the last ping it had.
            int ping = _roundTrips > 0
                ? (int)Math.Round(_roundTripSeconds / _roundTrips * 1000.0)
                : _quality.PingMilliseconds;
            float loss = _packetsIn > 0.0
                ? (float)(_packetsLostIn / _packetsIn)
                : _quality.PacketLoss;

            _quality = new ConnectionQuality(ping, loss, (float)(_bytesSent / elapsed), (float)(_bytesReceived / elapsed));

            _windowStart = now;
            _bytesSent = 0;
            _bytesReceived = 0;
            _roundTripSeconds = 0.0;
            _roundTrips = 0;
            _packetsIn *= LossDecay;
            _packetsLostIn *= LossDecay;
        }
    }
}
