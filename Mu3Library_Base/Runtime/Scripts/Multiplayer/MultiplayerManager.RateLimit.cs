using System.Collections.Generic;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Protocol;
using UnityEngine;

namespace Mu3Library.Multiplayer
{
    public partial class MultiplayerManager
    {
        /// <summary>
        /// What one account sent in the current window of the rate limits, and the requests past
        /// a limit that the host holds back for later windows.
        /// </summary>
        private sealed class RateState
        {
            /// <summary>When the window started; before the account's first packet, long enough ago that it ended.</summary>
            public double Start = double.NegativeInfinity;
            public int Packets;
            public long Bytes;

            /// <summary>True once a packet in this window went past a limit.</summary>
            public bool IsExceeded;

            /// <summary>True while every window since <see cref="ExceedingSince"/> went past a limit.</summary>
            public bool IsExceeding;
            public double ExceedingSince;

            /// <summary>Ready changes, load reports, and data requests waiting for room, oldest first; null until one waits.</summary>
            public Queue<DelayedRequest> Delayed;

            public int DelayedCount => Delayed != null ? Delayed.Count : 0;
        }

        /// <summary>A request the host took apart as it arrived, to apply once a window has room.</summary>
        private struct DelayedRequest
        {
            public PacketKind Kind;
            public int Bytes;
            public bool IsReady;
            public int GameNumber;
            public string Key;
            public string Value;
        }

        private enum RateDecision
        {
            /// <summary>Within the limits: handle it now.</summary>
            Handle,
            /// <summary>A game or chat message past a limit goes nowhere; a hello past it waits for room.</summary>
            Drop,
            /// <summary>A request past a limit, or behind one that waits: it waits for room.</summary>
            Delay,
            /// <summary>The account was kicked or disconnected, or the session ended while the host applied what waited.</summary>
            Kicked,
        }

        private const double RateWindowSeconds = 1.0;

        // By account and for the whole session, so neither connecting again nor leaving and joining
        // again starts a fresh window or a fresh count toward the kick.
        private readonly Dictionary<PlayerId, RateState> _rates = new Dictionary<PlayerId, RateState>();
        private readonly List<PlayerId> _delayingPlayers = new List<PlayerId>();



        private static bool IsDelayable(PacketKind kind)
        {
            return kind == PacketKind.SetReady || kind == PacketKind.SetParticipantData || kind == PacketKind.ReportLoaded;
        }

        private bool IsRateLimited => _rules.MaxMessagesPerSecond > 0 || _rules.MaxBytesPerSecond > 0;

        /// <summary>
        /// Counts a packet of <paramref name="player"/> against the rules' rate limits and decides
        /// what the host does with it. The requests that wait go first, as far as the current
        /// window has room, so a later packet never overtakes them. Past a limit, a game or chat
        /// message is dropped and a ready change, load report, or data request waits, up to
        /// <see cref="MultiplayerLimits.MaxDelayedRequests"/>; one more disconnects the account. The
        /// first packet past a limit in a window raises <see cref="OnRateLimitExceeded"/>, and an
        /// account past a limit in every window for the rules' kick time is kicked.
        /// </summary>
        private RateDecision CheckRateLimit(PlayerId player, PacketKind kind, int bytes)
        {
            bool isLimited = IsRateLimited;
            if (!_rates.TryGetValue(player, out RateState state))
            {
                // Without limits there is nothing to count, and nothing waits.
                if (!isLimited)
                {
                    return RateDecision.Handle;
                }

                state = new RateState();
                _rates.Add(player, state);
            }

            double now = Now;
            if (isLimited)
            {
                RollWindow(state, now);
            }

            if (state.DelayedCount > 0 && !ApplyDelayed(player, state))
            {
                return RateDecision.Kicked;
            }

            bool isDelayable = IsDelayable(kind);
            if (!isLimited)
            {
                return RateDecision.Handle;
            }

            if (state.DelayedCount == 0 && Fits(state, bytes, isDelayable || kind == PacketKind.Hello))
            {
                state.Packets++;
                state.Bytes += bytes;
                return RateDecision.Handle;
            }

            if (!ReportExcess(player, state, now))
            {
                return RateDecision.Kicked;
            }

            if (!isDelayable)
            {
                return RateDecision.Drop;
            }

            if (state.DelayedCount >= MultiplayerLimits.MaxDelayedRequests)
            {
                // The client counts on every request it sent, so one more cannot be dropped; it
                // goes, but may join again, as the rules may kick nobody.
                Debug.LogWarning($"[Multiplayer] Disconnected {player}: more than {MultiplayerLimits.MaxDelayedRequests} of its requests waited for the rate limit ({MultiplayerReason.RateLimited}).");
                DisconnectPlayer(player, MultiplayerReason.RateLimited);
                return RateDecision.Kicked;
            }

            return RateDecision.Delay;
        }

        /// <summary>Takes apart a request <see cref="CheckRateLimit"/> answered with <see cref="RateDecision.Delay"/>, to apply later.</summary>
        private void DelayRequest(PlayerId player, PacketKind kind, int bytes, ref PacketReader reader)
        {
            DelayedRequest request = new DelayedRequest { Kind = kind, Bytes = bytes };
            switch (kind)
            {
                case PacketKind.SetReady:
                    request.IsReady = reader.ReadBool();
                    break;

                case PacketKind.SetParticipantData:
                    ReadDataRequest(ref reader, out request.Key, out request.Value);
                    break;

                case PacketKind.ReportLoaded:
                    request.GameNumber = reader.ReadInt();
                    break;
            }

            RateState state = GetRateState(player);
            state.Delayed ??= new Queue<DelayedRequest>();
            state.Delayed.Enqueue(request);
        }

        /// <summary>
        /// Takes a hello that waited for room in the rate limits once the window has it, after the
        /// requests that wait; the rest of the handshake goes on from there.
        /// </summary>
        private void TakeDelayedHello(PeerConnection peer)
        {
            if (IsRateLimited && _rates.TryGetValue(peer.Player, out RateState state))
            {
                RollWindow(state, Now);
                if (state.DelayedCount > 0 || !Fits(state, peer.HelloBytes, takesOneOfAnySize: true))
                {
                    return;
                }

                state.Packets++;
                state.Bytes += peer.HelloBytes;
            }

            peer.IsHelloDelayed = false;
            peer.HasSaidHello = true;
            TryAdmit(peer);
        }

        /// <summary>Applies, once a frame, the requests that wait, as far as each account's window has room.</summary>
        private void UpdateDelayedRequests()
        {
            if (!_isHost || _rates.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<PlayerId, RateState> pair in _rates)
            {
                if (pair.Value.DelayedCount > 0)
                {
                    _delayingPlayers.Add(pair.Key);
                }
            }

            int generation = _sessionGeneration;
            bool isLimited = IsRateLimited;
            double now = Now;
            for (int i = 0; i < _delayingPlayers.Count && generation == _sessionGeneration; i++)
            {
                if (_rates.TryGetValue(_delayingPlayers[i], out RateState state) && state.DelayedCount > 0)
                {
                    if (isLimited)
                    {
                        RollWindow(state, now);
                    }

                    ApplyDelayed(_delayingPlayers[i], state);
                }
            }

            _delayingPlayers.Clear();
        }

        /// <summary>
        /// Applies the requests that wait, oldest first, while the current window has room, or
        /// all of them without a limit. False when the account left or the session ended meanwhile.
        /// </summary>
        private bool ApplyDelayed(PlayerId player, RateState state)
        {
            bool isLimited = IsRateLimited;
            int generation = _sessionGeneration;
            while (state.DelayedCount > 0)
            {
                DelayedRequest request = state.Delayed.Peek();
                if (isLimited && !Fits(state, request.Bytes, takesOneOfAnySize: true))
                {
                    return true;
                }

                state.Delayed.Dequeue();
                state.Packets++;
                state.Bytes += request.Bytes;

                switch (request.Kind)
                {
                    case PacketKind.SetReady:
                        HostApplyReady(player, request.IsReady);
                        break;

                    case PacketKind.SetParticipantData:
                        HostTakeParticipantData(player, request.Key, request.Value);
                        break;

                    case PacketKind.ReportLoaded:
                        HostTakeLoaded(player, request.GameNumber);
                        break;
                }

                if (generation != _sessionGeneration || IndexOfParticipant(player) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Whether a packet of this size still fits the window. A window that took nothing yet
        /// takes one request or hello of any size, so one larger than the byte limit waits only
        /// for a window of its own; a game or chat message that large never fits.
        /// </summary>
        private bool Fits(RateState state, int bytes, bool takesOneOfAnySize)
        {
            int maxPackets = _rules.MaxMessagesPerSecond;
            int maxBytes = _rules.MaxBytesPerSecond;
            return (maxPackets <= 0 || state.Packets < maxPackets)
                && (maxBytes <= 0 || state.Bytes + bytes <= maxBytes || (takesOneOfAnySize && state.Packets == 0));
        }

        private static void RollWindow(RateState state, double now)
        {
            if (now < state.Start + RateWindowSeconds)
            {
                return;
            }

            // The next window follows on from this one, unless a whole window went by without a
            // packet; a window within the limits, or such a gap, ends a run of excess.
            bool followsOn = now < state.Start + 2.0 * RateWindowSeconds;
            if (!followsOn || !state.IsExceeded)
            {
                state.IsExceeding = false;
            }

            state.Start = followsOn ? state.Start + RateWindowSeconds : now;
            state.Packets = 0;
            state.Bytes = 0;
            state.IsExceeded = false;
        }

        /// <summary>
        /// Notes a packet past a limit: the first in a window raises the event, and a run of excess
        /// as long as the rules' kick time kicks the account. False when it is gone after that.
        /// </summary>
        private bool ReportExcess(PlayerId player, RateState state, double now)
        {
            bool isFirstInWindow = !state.IsExceeded;
            state.IsExceeded = true;
            if (!state.IsExceeding)
            {
                state.IsExceeding = true;
                state.ExceedingSince = now;
            }

            float kickSeconds = _rules.RateLimitKickSeconds;
            if (kickSeconds > 0f && now - state.ExceedingSince >= kickSeconds)
            {
                Debug.LogWarning($"[Multiplayer] Kicked {player}: it went past the rate limit for {kickSeconds} seconds ({MultiplayerReason.RateLimited}).");
                KickPlayer(player, MultiplayerReason.RateLimited);
                return false;
            }

            if (isFirstInWindow && TryGetParticipant(player, out Participant sender))
            {
                int generation = _sessionGeneration;
                Raise(OnRateLimitExceeded, sender);

                // A handler may have kicked the sender or ended the session.
                if (generation != _sessionGeneration || IndexOfParticipant(player) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        private RateState GetRateState(PlayerId player)
        {
            if (!_rates.TryGetValue(player, out RateState state))
            {
                state = new RateState();
                _rates.Add(player, state);
            }

            return state;
        }

        /// <summary>
        /// Forgets what the account's requests still wait for, as its connection goes: the client
        /// takes its data from the host when it comes back, so it never counts on them.
        /// </summary>
        private void DiscardDelayedRequests(PlayerId player)
        {
            if (_rates.TryGetValue(player, out RateState state))
            {
                state.Delayed?.Clear();
            }
        }

        private void ResetRateState()
        {
            _rates.Clear();
            _delayingPlayers.Clear();
        }
    }
}
