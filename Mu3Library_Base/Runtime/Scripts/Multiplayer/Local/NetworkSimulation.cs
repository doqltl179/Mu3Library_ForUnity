using System;

namespace Mu3Library.Multiplayer.Local
{
    /// <summary>
    /// The worse network a <see cref="LocalNetwork"/> plays: how long each packet on a session
    /// connection takes, how far that strays, and how many packets get lost. Game packets and the
    /// backends' own quality probes draw from random sequences of their own, so a seeded network
    /// loses the same game packets however often the probes run.
    /// </summary>
    internal sealed class NetworkSimulation
    {
        /// <summary>
        /// A reliable packet that the loss takes again and again still arrives after this many
        /// resends, so even a loss of 100 % holds a session back instead of stalling it for good.
        /// </summary>
        public const int MaxResends = 10;

        private Random _packets = CreateRandom();
        private Random _probes = CreateRandom();

        public double LatencySeconds { get; set; }
        public double JitterSeconds { get; set; }

        /// <summary>Share of packets lost, from 0 to 1.</summary>
        public double LossRate { get; set; }

        public bool IsActive => LatencySeconds > 0.0 || JitterSeconds > 0.0 || LossRate > 0.0;

        /// <summary>A lost reliable packet is sent again once the sender could have heard it was missing: one round trip later.</summary>
        public double ResendSeconds => 2.0 * LatencySeconds;

        public void Seed(int seed)
        {
            _packets = new Random(seed);
            _probes = new Random(unchecked(seed * 31 + 17));
        }

        public bool DropsPacket() => Drops(_packets);
        public bool DropsProbe() => Drops(_probes);

        public double PacketDelay() => Delay(_packets);
        public double ProbeDelay() => Delay(_probes);

        /// <summary>How many times the loss takes a reliable packet before one of its sends gets through.</summary>
        public int ReliableResends()
        {
            int resends = 0;
            while (resends < MaxResends && Drops(_packets))
            {
                resends++;
            }

            return resends;
        }

        private bool Drops(Random random)
        {
            return LossRate > 0.0 && (LossRate >= 1.0 || random.NextDouble() < LossRate);
        }

        private double Delay(Random random)
        {
            if (JitterSeconds <= 0.0)
            {
                return LatencySeconds;
            }

            double delay = LatencySeconds + (random.NextDouble() * 2.0 - 1.0) * JitterSeconds;
            return delay > 0.0 ? delay : 0.0;
        }

        /// <summary>
        /// Seeded from a GUID rather than the clock, which processes that start in the same moment
        /// share, as <c>MachinePeer</c> picks its IDs.
        /// </summary>
        private static Random CreateRandom() => new Random(Guid.NewGuid().GetHashCode());
    }
}
