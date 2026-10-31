using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Protocol;
using UnityEngine;

namespace Mu3Library.Multiplayer
{
    public partial class MultiplayerManager
    {
        /// <summary>What one client sent in the current window of the rate limits.</summary>
        private sealed class RateWindow
        {
            /// <summary>When the window started; before the client's first packet, long enough ago that it ended.</summary>
            public double Start = double.NegativeInfinity;
            public int Packets;
            public long Bytes;

            /// <summary>True once a packet in this window went past a limit.</summary>
            public bool IsExceeded;

            /// <summary>True while every window since <see cref="ExceedingSince"/> went past a limit.</summary>
            public bool IsExceeding;
            public double ExceedingSince;
        }

        private const double RateWindowSeconds = 1.0;


        /// <summary>
        /// Counts a packet a client sent after its welcome against the rules' rate limits, and
        /// answers whether the host goes on to handle it. A game or chat message past a limit is
        /// dropped. A ready change, a load report, or a participant data request is taken all the
        /// same, since the client goes on from every request it sent, but it counts and is
        /// reported like any other.
        /// False too when the packet's sender was kicked, here or by a handler of the report.
        /// </summary>
        private bool TakeWithinRateLimit(PeerConnection peer, PacketKind kind, int bytes)
        {
            int maxPackets = _rules.MaxMessagesPerSecond;
            int maxBytes = _rules.MaxBytesPerSecond;
            if (maxPackets <= 0 && maxBytes <= 0)
            {
                return true;
            }

            RateWindow window = peer.Rate;
            double now = Now;
            if (now >= window.Start + RateWindowSeconds)
            {
                // The next window follows on from this one, unless a whole window went by without
                // a packet; a window within the limits, or such a gap, ends a run of excess.
                bool followsOn = now < window.Start + 2.0 * RateWindowSeconds;
                if (!followsOn || !window.IsExceeded)
                {
                    window.IsExceeding = false;
                }

                window.Start = followsOn ? window.Start + RateWindowSeconds : now;
                window.Packets = 0;
                window.Bytes = 0;
                window.IsExceeded = false;
            }

            bool isWithin = (maxPackets <= 0 || window.Packets < maxPackets)
                && (maxBytes <= 0 || window.Bytes + bytes <= maxBytes);
            bool mayDrop = kind != PacketKind.SetReady && kind != PacketKind.SetParticipantData && kind != PacketKind.ReportLoaded;
            if (isWithin || !mayDrop)
            {
                window.Packets++;
                window.Bytes += bytes;
            }

            if (isWithin)
            {
                return true;
            }

            bool isFirstInWindow = !window.IsExceeded;
            window.IsExceeded = true;
            if (!window.IsExceeding)
            {
                window.IsExceeding = true;
                window.ExceedingSince = now;
            }

            float kickSeconds = _rules.RateLimitKickSeconds;
            if (kickSeconds > 0f && now - window.ExceedingSince >= kickSeconds)
            {
                Debug.LogWarning($"[Multiplayer] Kicked {peer.Player}: it went past the rate limit for {kickSeconds} seconds ({MultiplayerReason.RateLimited}).");
                KickPlayer(peer.Player, MultiplayerReason.RateLimited);
                return false;
            }

            if (isFirstInWindow && TryGetParticipant(peer.Player, out Participant sender))
            {
                int generation = _sessionGeneration;
                Raise(OnRateLimitExceeded, sender);

                // A handler may have kicked the sender or ended the session.
                if (generation != _sessionGeneration || !_peers.ContainsKey(peer.Connection))
                {
                    return false;
                }
            }

            return !mayDrop;
        }
    }
}
