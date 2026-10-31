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
        private readonly List<PlayerId> _shortenedReservations = new List<PlayerId>();

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

            DiscardDelayedRequests(player);
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
            Participant kept = _participants[index];
            _reservations.Remove(player);

            // A new start of the game, rather than one that kept running, loads its game again.
            bool isLoadReset = !peer.IsReturning && kept.IsLoaded;
            Participant returned = kept.WithConnected(true).WithLoaded(kept.IsLoaded && !isLoadReset);
            _participants[index] = returned;

            if (!SendWelcome(peer))
            {
                // As if this attempt never arrived: the slot stays kept until its time.
                _participants[index] = kept;
                _reservations[player] = deadline;
                RefusePeer(peer, MultiplayerReason.ConnectionLost);
                return true;
            }

            peer.IsWelcomed = true;
            int sessionGeneration = _sessionGeneration;
            if (isLoadReset)
            {
                WaitAgainFor(player);
                WriteParticipantUpdated(returned);
                BroadcastToWelcomed(peer.Connection);
                if (sessionGeneration != _sessionGeneration)
                {
                    return true;
                }

                Raise(OnParticipantChanged, returned);
                if (sessionGeneration != _sessionGeneration)
                {
                    return true;
                }
            }

            _writer.Begin(PacketKind.ParticipantReconnected);
            _writer.WriteULong(player.Value);
            BroadcastToWelcomed(peer.Connection);
            if (sessionGeneration == _sessionGeneration)
            {
                Raise(OnParticipantReconnected, returned);
            }

            return true;
        }

        /// <summary>
        /// Shortens the slots kept now to at most the reservation time that new rules set, from
        /// now on; a longer time leaves them as they are.
        /// </summary>
        private void ShortenReservations(float seconds)
        {
            if (_reservations.Count == 0)
            {
                return;
            }

            double latest = Now + seconds;
            foreach (KeyValuePair<PlayerId, double> pair in _reservations)
            {
                if (pair.Value > latest)
                {
                    _shortenedReservations.Add(pair.Key);
                }
            }

            for (int i = 0; i < _shortenedReservations.Count; i++)
            {
                _reservations[_shortenedReservations[i]] = latest;
            }

            _shortenedReservations.Clear();
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
        /// its slot; otherwise the session ends with <paramref name="reason"/>. A connection that
        /// breaks after the lobby said the host left ends the session with
        /// <see cref="MultiplayerReason.HostLeft"/>, as there is no host to come back to.
        /// </summary>
        private void LoseHost(MultiplayerReason reason)
        {
            if (_isAwaitingHostClose)
            {
                EndSession(MultiplayerReason.HostLeft, raiseEvents: true);
                return;
            }

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

            if (IsHostGoneFromLobby())
            {
                EndReconnect(MultiplayerReason.HostLeft);
                return;
            }

            double now = Now;
            if (now >= _reconnectDeadline)
            {
                EndReconnect(MultiplayerReason.ConnectionLost);
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
        /// Whether the lobby, as this player sees it now, says that the host is gone: it passed
        /// to another member, or it no longer lists the host. A lobby that no longer lists this
        /// player tells nothing, as a Steam lobby may drop a member whose own network went down.
        /// </summary>
        private bool IsHostGoneFromLobby()
        {
            if (!_lobbyId.IsValid || !_hostId.IsValid || !_backend.IsLobbyMember(_lobbyId, LocalPlayerId))
            {
                return false;
            }

            PlayerId owner = _backend.GetLobbyOwner(_lobbyId);
            return (owner.IsValid && owner != _hostId) || !_backend.IsLobbyMember(_lobbyId, _hostId);
        }

        /// <summary>
        /// Gives up coming back. An attempt still on its way closes as lost, so the host keeps
        /// the slot until its own time rather than taking it for a player who left.
        /// </summary>
        private void EndReconnect(MultiplayerReason reason)
        {
            if (_hostConnection.IsValid)
            {
                _backend.Disconnect(_hostConnection, ReasonCodes.ToWire(MultiplayerReason.ConnectionLost));
                _hostConnection = ConnectionId.None;
            }

            EndSession(reason, raiseEvents: true);
        }

        /// <summary>
        /// Raises what a returning player missed and the return itself, once the state shows
        /// where the session is now: the start of another game, or the end of its own.
        /// </summary>
        private void HandleReturned(SessionState state, bool isSameGame)
        {
            int generation = _sessionGeneration;

            // A load report still on its way when the connection dropped is lost; it goes again.
            if (isSameGame && _reportedLoadedGame == _gameNumber
                && TryGetParticipant(LocalPlayerId, out Participant returning) && !returning.IsLoaded)
            {
                _writer.Begin(PacketKind.ReportLoaded).WriteInt(_gameNumber);
                if (!SendToHost(DeliveryMode.Reliable) || generation != _sessionGeneration || _state != state)
                {
                    return;
                }
            }

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
