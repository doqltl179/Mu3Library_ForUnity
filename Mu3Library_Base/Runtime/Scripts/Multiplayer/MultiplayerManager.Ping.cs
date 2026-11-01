using System;
using System.Collections.Generic;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Protocol;

namespace Mu3Library.Multiplayer
{
    public partial class MultiplayerManager
    {
        // The bytes of one entry of a ping report: the player, and the round trip in milliseconds.
        private const int PingEntrySize = sizeof(ulong) + sizeof(ushort);

        // On the host, when it reports the pings next; infinity until its session runs.
        private double _nextPingReport = double.PositiveInfinity;
        private readonly List<(PlayerId Player, int Ping)> _pingScratch = new List<(PlayerId Player, int Ping)>();

        // On a client, the round trip between the host and each player with a connection, as
        // the host last reported it.
        private readonly Dictionary<PlayerId, int> _reportedPings = new Dictionary<PlayerId, int>();



        public bool TryGetParticipantPing(PlayerId player, out int pingMilliseconds)
        {
            pingMilliseconds = 0;
            if (!_isInitialized || _isDisposed || !player.IsValid || (!_isHost && !_isWelcomed))
            {
                return false;
            }

            int index = IndexOfParticipant(player);
            if (index < 0 || !_participants[index].IsConnected)
            {
                return false;
            }

            if (player == _hostId)
            {
                return true;
            }

            if (!_isHost)
            {
                return _reportedPings.TryGetValue(player, out pingMilliseconds);
            }

            PeerConnection peer = FindWelcomedPeer(player);
            if (peer == null || !_backend.TryGetConnectionQuality(peer.Connection, out ConnectionQuality quality))
            {
                return false;
            }

            pingMilliseconds = quality.PingMilliseconds;
            return true;
        }

        #region Host
        /// <summary>
        /// Tells every client, once every <see cref="MultiplayerLimits.PingReportIntervalSeconds"/>,
        /// the round trip to each player with a connection, as the backend measures it now.
        /// </summary>
        private void UpdatePingReports()
        {
            if (!_isHost || (_state != SessionState.Lobby && _state != SessionState.InGame))
            {
                return;
            }

            double now = Now;
            if (double.IsPositiveInfinity(_nextPingReport))
            {
                _nextPingReport = now + MultiplayerLimits.PingReportIntervalSeconds;
                return;
            }

            if (now < _nextPingReport)
            {
                return;
            }

            // Keeps the beat, but a frame longer than the interval sends no second report to catch up.
            _nextPingReport += MultiplayerLimits.PingReportIntervalSeconds;
            if (_nextPingReport <= now)
            {
                _nextPingReport = now + MultiplayerLimits.PingReportIntervalSeconds;
            }

            _pingScratch.Clear();
            foreach (PeerConnection peer in _peers.Values)
            {
                if (peer.IsWelcomed && _backend.TryGetConnectionQuality(peer.Connection, out ConnectionQuality quality))
                {
                    _pingScratch.Add((peer.Player, quality.PingMilliseconds));
                }
            }

            if (_pingScratch.Count == 0)
            {
                return;
            }

            _writer.Begin(PacketKind.ParticipantPings);
            _writer.WriteByte((byte)_pingScratch.Count);
            for (int i = 0; i < _pingScratch.Count; i++)
            {
                _writer.WriteULong(_pingScratch[i].Player.Value);
                _writer.WriteUShort((ushort)Math.Min(Math.Max(_pingScratch[i].Ping, 0), ushort.MaxValue));
            }

            // Unreliable, since the next report replaces this one: a lost report costs no resend
            // and never counts as a connection the host could not reach.
            foreach (PeerConnection peer in _peers.Values)
            {
                if (peer.IsWelcomed)
                {
                    SendToPeer(peer, DeliveryMode.Unreliable);
                }
            }
        }
        #endregion

        #region Client
        private void HandleParticipantPings(ref PacketReader reader)
        {
            int count = reader.ReadByte();
            if (count > MultiplayerLimits.MaxCapacity || reader.Remaining != count * PingEntrySize)
            {
                throw new FormatException($"Invalid ping report of {count} players in {reader.Remaining} bytes.");
            }

            // Each report names every player the host has a connection to, so it replaces the last.
            _reportedPings.Clear();
            for (int i = 0; i < count; i++)
            {
                PlayerId player = new PlayerId(reader.ReadULong());
                _reportedPings[player] = reader.ReadUShort();
            }
        }
        #endregion

        private void ResetPingState()
        {
            _nextPingReport = double.PositiveInfinity;
            _pingScratch.Clear();
            _reportedPings.Clear();
        }
    }
}
