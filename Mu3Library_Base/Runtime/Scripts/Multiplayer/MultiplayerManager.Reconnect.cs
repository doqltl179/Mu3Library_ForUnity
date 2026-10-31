using System.Collections.Generic;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Protocol;

namespace Mu3Library.Multiplayer
{
    public partial class MultiplayerManager
    {
        // How long a client waits between two attempts to reach its host again.
        private const double ReconnectRetrySeconds = 1.0;

        // On the host, the players whose connection dropped during a game, and when their slot frees.
        private readonly Dictionary<PlayerId, double> _reservations = new Dictionary<PlayerId, double>();
        private readonly List<PlayerId> _expiredReservations = new List<PlayerId>();

        // On a client that reconnects, when it gives up, and when it tries the next time.
        private double _reconnectDeadline = 0d;
        private double _nextReconnectAttempt = 0d;



        #region Host
        /// <summary>
        /// A welcomed player's connection broke without the player leaving. During a game, the
        /// host keeps their slot for the rules' reservation time and tells everyone they are away;
        /// otherwise they leave with <see cref="MultiplayerReason.ConnectionLost"/>.
        /// </summary>
        private void LosePlayer(PlayerId player)
        {
            int index = IndexOfParticipant(player);
            if (index < 0)
            {
                return;
            }

            float seconds = _rules.ReconnectReservationSeconds;
            if (_state != SessionState.InGame || !(seconds > 0f))
            {
                RemoveParticipant(player, MultiplayerReason.ConnectionLost);
                return;
            }

            _reservations[player] = Now + seconds;
            Participant away = _participants[index].WithConnected(false);
            _participants[index] = away;

            _writer.Begin(PacketKind.ParticipantDisconnected);
            _writer.WriteULong(player.Value);

            int generation = _sessionGeneration;
            BroadcastToWelcomed(ConnectionId.None);
            if (generation == _sessionGeneration)
            {
                Raise(OnParticipantDisconnected, away);
            }
        }

        /// <summary>
        /// Takes a player back into the slot the host keeps for them, whether their hello says
        /// they return or they joined again from the start, since that account was let in before;
        /// the lobby need not list them, as Steam may drop a member whose network went down. A
        /// hello that says it returns finds the slot gone, or finds the account still connected
        /// when the host has not noticed yet that the old connection broke, which then ends. True
        /// when the hello was settled here.
        /// </summary>
        private bool TryReturnToSlot(PeerConnection peer)
        {
            PlayerId player = peer.Player;
            if (peer.IsReturning && !_reservations.ContainsKey(player))
            {
                PeerConnection stale = FindWelcomedPeer(player);
                if (stale != null)
                {
                    int generation = _sessionGeneration;
                    _peers.Remove(stale.Connection);
                    _backend.Disconnect(stale.Connection, ReasonCodes.ToWire(MultiplayerReason.ConnectionLost));
                    LosePlayer(player);

                    // A handler may have ended the session.
                    if (generation != _sessionGeneration || !_peers.ContainsKey(peer.Connection))
                    {
                        return true;
                    }
                }
            }

            if (!_reservations.TryGetValue(player, out double deadline))
            {
                if (!peer.IsReturning)
                {
                    return false;
                }

                // The time ran out, the host kicked the player, or the session kept no slot.
                RefusePeer(peer, _bannedPlayers.Contains(player) ? MultiplayerReason.Banned : MultiplayerReason.ConnectionLost);
                return true;
            }

            int index = IndexOfParticipant(player);
            _reservations.Remove(player);
            Participant returned = _participants[index].WithConnected(true);
            _participants[index] = returned;

            if (!SendWelcome(peer))
            {
                // As if this attempt never arrived: the slot stays kept until its time.
                _participants[index] = returned.WithConnected(false);
                _reservations[player] = deadline;
                RefusePeer(peer, MultiplayerReason.ConnectionLost);
                return true;
            }

            peer.IsWelcomed = true;

            _writer.Begin(PacketKind.ParticipantReconnected);
            _writer.WriteULong(player.Value);

            int sessionGeneration = _sessionGeneration;
            BroadcastToWelcomed(peer.Connection);
            if (sessionGeneration == _sessionGeneration)
            {
                Raise(OnParticipantReconnected, returned);
            }

            return true;
        }

        /// <summary>Frees the slots whose player did not come back in time.</summary>
        private void UpdateReservations()
        {
            if (!_isHost || _reservations.Count == 0)
            {
                return;
            }

            double now = Now;
            foreach (KeyValuePair<PlayerId, double> pair in _reservations)
            {
                if (now >= pair.Value)
                {
                    _expiredReservations.Add(pair.Key);
                }
            }

            int generation = _sessionGeneration;
            for (int i = 0; i < _expiredReservations.Count && generation == _sessionGeneration; i++)
            {
                if (_reservations.ContainsKey(_expiredReservations[i]))
                {
                    RemoveParticipant(_expiredReservations[i], MultiplayerReason.ConnectionLost);
                }
            }

            _expiredReservations.Clear();
        }
        #endregion

        #region Client
        /// <summary>
        /// This player's connection to the host broke, or a reliable packet could not leave on it.
        /// During a game with a reservation time in the rules, this player starts coming back to
        /// its slot; otherwise the session ends with <paramref name="reason"/>.
        /// </summary>
        private void LoseHost(MultiplayerReason reason)
        {
            float seconds = _rules.ReconnectReservationSeconds;
            if (_state != SessionState.InGame || !(seconds > 0f))
            {
                EndSession(reason, raiseEvents: true);
                return;
            }

            // The host takes the close as a lost connection and keeps the slot.
            if (_hostConnection.IsValid)
            {
                _backend.Disconnect(_hostConnection, ReasonCodes.ToWire(MultiplayerReason.ConnectionLost));
                _hostConnection = ConnectionId.None;
            }

            _isWelcomed = false;
            double now = Now;
            _reconnectDeadline = now + seconds;
            _nextReconnectAttempt = now;

            int index = IndexOfParticipant(LocalPlayerId);
            if (index >= 0)
            {
                _participants[index] = _participants[index].WithConnected(false);
            }

            SetState(SessionState.Reconnecting, MultiplayerReason.ConnectionLost);
        }

        /// <summary>
        /// Connects to the host again, at most once every <see cref="ReconnectRetrySeconds"/>,
        /// until the host takes this player back or the reservation time is over.
        /// </summary>
        private void UpdateReconnect()
        {
            if (_state != SessionState.Reconnecting || _isHost)
            {
                return;
            }

            double now = Now;
            if (now >= _reconnectDeadline)
            {
                EndSession(MultiplayerReason.ConnectionLost, raiseEvents: true);
                return;
            }

            if (_hostConnection.IsValid || now < _nextReconnectAttempt)
            {
                return;
            }

            _nextReconnectAttempt = now + ReconnectRetrySeconds;
            _hostConnection = _backend.Connect(_hostId);
        }

        /// <summary>
        /// Raises what a returning player missed and the return itself, once the state shows
        /// where the session is now: the start of another game, or the end of its own.
        /// </summary>
        private void HandleReturned(SessionState state, bool isSameGame)
        {
            int generation = _sessionGeneration;
            if (state == SessionState.InGame && !isSameGame)
            {
                Raise(OnGameStarted, new System.ReadOnlyMemory<byte>(_startData));
            }
            else if (state == SessionState.Lobby)
            {
                Raise(OnReturnedToLobby);
            }

            if (generation == _sessionGeneration && TryGetParticipant(LocalPlayerId, out Participant self))
            {
                Raise(OnParticipantReconnected, self);
            }
        }

        private void HandleParticipantConnection(ref PacketReader reader, bool isConnected)
        {
            PlayerId player = new PlayerId(reader.ReadULong());
            int index = IndexOfParticipant(player);
            if (index < 0 || _participants[index].IsConnected == isConnected)
            {
                return;
            }

            Participant updated = _participants[index].WithConnected(isConnected);
            _participants[index] = updated;
            Raise(isConnected ? OnParticipantReconnected : OnParticipantDisconnected, updated);
        }
        #endregion
    }
}
