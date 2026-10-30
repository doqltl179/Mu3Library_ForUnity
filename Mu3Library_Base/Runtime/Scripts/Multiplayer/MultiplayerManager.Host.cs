using System;
using System.Collections.Generic;
using System.Globalization;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Protocol;
using UnityEngine;

namespace Mu3Library.Multiplayer
{
    public partial class MultiplayerManager
    {
        private sealed class PeerConnection
        {
            public ConnectionId Connection;
            public PlayerId Player;

            /// <summary>True once the host accepted the player's hello and added them to the roster.</summary>
            public bool IsWelcomed;

            public double HandshakeDeadline;
        }

        private readonly Dictionary<ConnectionId, PeerConnection> _peers = new Dictionary<ConnectionId, PeerConnection>();
        private readonly List<PeerConnection> _expiredPeers = new List<PeerConnection>();
        private readonly List<PlayerId> _notReadyScratch = new List<PlayerId>();

        // Players the host kicked from this session; they cannot come back until it ends.
        private readonly HashSet<PlayerId> _bannedPlayers = new HashSet<PlayerId>();



        #region Create
        public void CreateLobby(LobbyCreateOptions options, Action<MultiplayerResult> callback = null)
        {
            MultiplayerReason check = CheckCanStartSession();
            if (check == MultiplayerReason.None)
            {
                check = options == null ? MultiplayerReason.InvalidArgument : ValidateRules(options.Rules, 1);
            }

            if (check == MultiplayerReason.None)
            {
                check = ValidateLobbyOptions(options);
            }

            if (check != MultiplayerReason.None)
            {
                Invoke(callback, MultiplayerResult.Failure(check));
                return;
            }

            if (!_backend.StartListening())
            {
                Invoke(callback, MultiplayerResult.Failure(MultiplayerReason.ConnectionFailed));
                return;
            }

            _isHost = true;
            _hostId = LocalPlayerId;
            _rules = options.Rules;
            _pendingCallback = callback;

            Dictionary<string, string> data = BuildLobbyData(options);
            int generation = _sessionGeneration;

            SetState(SessionState.Creating, MultiplayerReason.None);
            if (generation != _sessionGeneration)
            {
                return;
            }

            _backend.CreateLobby(options.Visibility, options.Rules.Capacity, data,
                (reason, lobby) => HandleLobbyCreated(generation, reason, lobby));
        }

        private void HandleLobbyCreated(int generation, MultiplayerReason reason, LobbyId lobby)
        {
            if (generation != _sessionGeneration || _state != SessionState.Creating)
            {
                // The session ended while the lobby was being made; nobody hosts it any more.
                if (reason == MultiplayerReason.None && lobby.IsValid)
                {
                    _backend.LeaveLobby(lobby);
                }

                return;
            }

            if (reason != MultiplayerReason.None || !lobby.IsValid)
            {
                EndSession(reason == MultiplayerReason.None ? MultiplayerReason.BackendError : reason, raiseEvents: true);
                return;
            }

            _lobbyId = lobby;
            _participants.Add(new Participant(LocalPlayerId, GetParticipantName(LocalPlayerId), isHost: true, isLocal: true, isReady: false, isLateJoiner: false));

            // The callback stays pending through the state event: a handler that ends the session
            // there answers it with the reason, and only a session that survives answers success.
            SetState(SessionState.Lobby, MultiplayerReason.None);
            if (generation == _sessionGeneration)
            {
                Action<MultiplayerResult> callback = _pendingCallback;
                _pendingCallback = null;
                Invoke(callback, MultiplayerResult.Success);
            }
        }

        private Dictionary<string, string> BuildLobbyData(LobbyCreateOptions options)
        {
            Dictionary<string, string> data = new Dictionary<string, string>(options.Data);
            data[LobbyDataKeys.GameId] = _gameId;
            data[LobbyDataKeys.ProtocolVersion] = _protocolVersionText;
            data[LobbyDataKeys.HostId] = LocalPlayerId.Value.ToString(CultureInfo.InvariantCulture);
            data[LobbyDataKeys.Name] = options.Name ?? string.Empty;
            data[LobbyDataKeys.State] = LobbyDataKeys.StateLobby;
            return data;
        }

        private static MultiplayerReason ValidateLobbyOptions(LobbyCreateOptions options)
        {
            if (options.Name != null && options.Name.Length > LobbyCreateOptions.MaxNameLength)
            {
                Debug.LogWarning($"[Multiplayer] A lobby name holds at most {LobbyCreateOptions.MaxNameLength} characters.");
                return MultiplayerReason.InvalidArgument;
            }

            foreach (KeyValuePair<string, string> pair in options.Data)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Value == null || LobbyDataKeys.IsReserved(pair.Key))
                {
                    Debug.LogWarning($"[Multiplayer] Lobby data key '{pair.Key}' is empty, has no value, or uses the reserved '{LobbyDataKeys.Prefix}' prefix.");
                    return MultiplayerReason.InvalidArgument;
                }
            }

            return MultiplayerReason.None;
        }

        private static MultiplayerReason ValidateRules(SessionRules rules, int playerCount)
        {
            if (rules.Capacity < MultiplayerLimits.MinCapacity
                || rules.Capacity > MultiplayerLimits.MaxCapacity
                || rules.Capacity < playerCount)
            {
                return MultiplayerReason.InvalidCapacity;
            }

            if (rules.MinPlayersToStart < 1 || rules.MinPlayersToStart > rules.Capacity)
            {
                return MultiplayerReason.InvalidArgument;
            }

            return MultiplayerReason.None;
        }
        #endregion

        #region Host Requests
        public MultiplayerResult SetReady(bool isReady)
        {
            if (!_isInitialized || _isDisposed)
            {
                return MultiplayerResult.Failure(MultiplayerReason.NotInitialized);
            }

            if (_state != SessionState.Lobby)
            {
                return MultiplayerResult.Failure(MultiplayerReason.InvalidState);
            }

            if (_isHost)
            {
                HostApplyReady(LocalPlayerId, isReady);
                return MultiplayerResult.Success;
            }

            // The host decides; this player's own flag changes when the host reports it back.
            _writer.Begin(PacketKind.SetReady).WriteBool(isReady);
            return _backend.Send(_hostConnection, _writer.Written, DeliveryMode.Reliable)
                ? MultiplayerResult.Success
                : MultiplayerResult.Failure(MultiplayerReason.ConnectionLost);
        }

        public MultiplayerResult UpdateRules(SessionRules rules)
        {
            MultiplayerReason check = CheckHostRequest(SessionState.Lobby);
            if (check == MultiplayerReason.None)
            {
                check = ValidateRules(rules, _participants.Count);
            }

            if (check != MultiplayerReason.None)
            {
                return MultiplayerResult.Failure(check);
            }

            if (rules.Capacity != _rules.Capacity && !_backend.SetLobbyCapacity(_lobbyId, rules.Capacity))
            {
                return MultiplayerResult.Failure(MultiplayerReason.BackendError);
            }

            _rules = rules;

            _writer.Begin(PacketKind.RulesChanged);
            WriteRules(_writer, rules);
            BroadcastToWelcomed(ConnectionId.None);

            Raise(OnRulesChanged, rules);
            return MultiplayerResult.Success;
        }

        public MultiplayerResult Kick(PlayerId player)
        {
            MultiplayerReason check = CheckHostRequest(null);
            if (check != MultiplayerReason.None)
            {
                return MultiplayerResult.Failure(check);
            }

            if (player == LocalPlayerId || IndexOfParticipant(player) < 0)
            {
                return MultiplayerResult.Failure(MultiplayerReason.InvalidArgument);
            }

            _bannedPlayers.Add(player);

            PeerConnection peer = FindWelcomedPeer(player);
            if (peer != null)
            {
                _peers.Remove(peer.Connection);
                _backend.Disconnect(peer.Connection, ReasonCodes.ToWire(MultiplayerReason.Kicked));
            }

            RemoveParticipant(player, MultiplayerReason.Kicked);
            return MultiplayerResult.Success;
        }

        public StartGameResult StartGame(ReadOnlySpan<byte> startData = default)
        {
            MultiplayerReason check = CheckHostRequest(SessionState.Lobby);
            if (check != MultiplayerReason.None)
            {
                return new StartGameResult(check, null);
            }

            if (startData.Length > MultiplayerLimits.MaxStartDataSize)
            {
                return new StartGameResult(MultiplayerReason.MessageTooLarge, null);
            }

            if (_participants.Count < _rules.MinPlayersToStart)
            {
                return new StartGameResult(MultiplayerReason.StartConditionNotMet, null);
            }

            if (_rules.RequireReady)
            {
                _notReadyScratch.Clear();
                for (int i = 0; i < _participants.Count; i++)
                {
                    Participant participant = _participants[i];
                    if (!participant.IsHost && !participant.IsReady)
                    {
                        _notReadyScratch.Add(participant.Id);
                    }
                }

                if (_notReadyScratch.Count > 0)
                {
                    PlayerId[] notReady = _notReadyScratch.ToArray();
                    _notReadyScratch.Clear();
                    return new StartGameResult(MultiplayerReason.StartConditionNotMet, notReady);
                }
            }

            _startData = startData.ToArray();

            _backend.SetLobbyData(_lobbyId, LobbyDataKeys.State, LobbyDataKeys.StateInGame);
            if (!_rules.AllowLateJoin)
            {
                _backend.SetLobbyJoinable(_lobbyId, false);
            }

            _writer.Begin(PacketKind.GameStarted).WriteBytes(_startData);
            BroadcastToWelcomed(ConnectionId.None);

            int generation = _sessionGeneration;
            SetState(SessionState.InGame, MultiplayerReason.None);
            if (generation == _sessionGeneration)
            {
                Raise(OnGameStarted, new ReadOnlyMemory<byte>(_startData));
            }

            return new StartGameResult(MultiplayerReason.None, null);
        }

        public MultiplayerResult EndGame()
        {
            MultiplayerReason check = CheckHostRequest(SessionState.InGame);
            if (check != MultiplayerReason.None)
            {
                return MultiplayerResult.Failure(check);
            }

            ReturnRosterToLobby();

            _backend.SetLobbyData(_lobbyId, LobbyDataKeys.State, LobbyDataKeys.StateLobby);
            _backend.SetLobbyJoinable(_lobbyId, true);

            _writer.Begin(PacketKind.ReturnedToLobby);
            BroadcastToWelcomed(ConnectionId.None);

            int generation = _sessionGeneration;
            SetState(SessionState.Lobby, MultiplayerReason.None);
            if (generation == _sessionGeneration)
            {
                Raise(OnReturnedToLobby);
            }

            return MultiplayerResult.Success;
        }

        /// <summary>
        /// Checks that this player hosts a running session, and, when <paramref name="requiredState"/>
        /// is given, that the session is in that state.
        /// </summary>
        private MultiplayerReason CheckHostRequest(SessionState? requiredState)
        {
            if (!_isInitialized || _isDisposed)
            {
                return MultiplayerReason.NotInitialized;
            }

            if (_state != SessionState.Lobby && _state != SessionState.InGame)
            {
                return MultiplayerReason.InvalidState;
            }

            if (!_isHost)
            {
                return MultiplayerReason.NotHost;
            }

            if (requiredState.HasValue && _state != requiredState.Value)
            {
                return MultiplayerReason.InvalidState;
            }

            return MultiplayerReason.None;
        }

        private void ReturnRosterToLobby()
        {
            _startData = Array.Empty<byte>();
            for (int i = 0; i < _participants.Count; i++)
            {
                _participants[i] = _participants[i].WithReady(false).WithLateJoiner(false);
            }
        }
        #endregion

        #region Host Connections
        private void HostHandleConnected(ConnectionId connection, PlayerId remote)
        {
            if (!_isHost || (_state != SessionState.Lobby && _state != SessionState.InGame))
            {
                _backend.Disconnect(connection, ReasonCodes.ToWire(MultiplayerReason.LobbyNotFound));
                return;
            }

            _peers[connection] = new PeerConnection
            {
                Connection = connection,
                Player = remote,
                IsWelcomed = false,
                HandshakeDeadline = Now + _handshakeTimeoutSeconds,
            };
        }

        private void HostHandleDisconnected(ConnectionId connection, BackendDisconnectKind kind, int reasonCode)
        {
            if (!_peers.TryGetValue(connection, out PeerConnection peer))
            {
                return;
            }

            _peers.Remove(connection);
            if (!peer.IsWelcomed)
            {
                return;
            }

            // A client can only leave on its own; any other reason it names would let it show the
            // others something the host never decided, such as a kick.
            MultiplayerReason reason = kind == BackendDisconnectKind.ClosedByPeer
                && ReasonCodes.FromWire(reasonCode) == MultiplayerReason.LeftNormally
                    ? MultiplayerReason.LeftNormally
                    : MultiplayerReason.ConnectionLost;

            RemoveParticipant(peer.Player, reason);
        }

        private void HostHandleReceived(ConnectionId connection, PlayerId remote, ReadOnlySpan<byte> data)
        {
            if (!_peers.TryGetValue(connection, out PeerConnection peer))
            {
                return;
            }

            PacketReader reader = new PacketReader(data);
            PacketKind kind = reader.ReadKind();

            if (!peer.IsWelcomed)
            {
                if (kind != PacketKind.Hello)
                {
                    Debug.LogWarning($"[Multiplayer] Dropped {kind} from {remote} before its hello.");
                    return;
                }

                string gameId = reader.ReadString();
                int protocolVersion = reader.ReadInt();
                HandleHello(peer, gameId, protocolVersion);
                return;
            }

            switch (kind)
            {
                case PacketKind.SetReady:
                    HostApplyReady(peer.Player, reader.ReadBool());
                    break;

                default:
                    // Only the host sends the other kinds; a client that sends one is ignored.
                    Debug.LogWarning($"[Multiplayer] Dropped {kind} from client {remote}.");
                    break;
            }
        }

        private void HandleHello(PeerConnection peer, string gameId, int protocolVersion)
        {
            MultiplayerReason reject = MultiplayerReason.None;
            if (!string.Equals(gameId, _gameId, StringComparison.Ordinal) || protocolVersion != _protocolVersion)
            {
                reject = MultiplayerReason.VersionMismatch;
            }
            else if (_bannedPlayers.Contains(peer.Player))
            {
                reject = MultiplayerReason.Banned;
            }
            else if (peer.Player == LocalPlayerId || IndexOfParticipant(peer.Player) >= 0)
            {
                reject = MultiplayerReason.DuplicateAccount;
            }
            else if (_state == SessionState.InGame && !_rules.AllowLateJoin)
            {
                reject = MultiplayerReason.GameInProgress;
            }
            else if (_participants.Count >= _rules.Capacity)
            {
                // The lobby's own capacity normally stops this earlier; two players entering at
                // nearly the same moment are stopped here.
                reject = MultiplayerReason.SessionFull;
            }

            if (reject != MultiplayerReason.None)
            {
                _peers.Remove(peer.Connection);
                _backend.Disconnect(peer.Connection, ReasonCodes.ToWire(reject));
                return;
            }

            Participant joined = new Participant(
                peer.Player,
                GetParticipantName(peer.Player),
                isHost: false,
                isLocal: false,
                isReady: false,
                isLateJoiner: _state == SessionState.InGame);
            _participants.Add(joined);

            _writer.Begin(PacketKind.Welcome);
            _writer.WriteByte((byte)_state);
            _writer.WriteULong(_hostId.Value);
            WriteRules(_writer, _rules);
            _writer.WriteBytes(_startData);
            _writer.WriteInt(_participants.Count);
            for (int i = 0; i < _participants.Count; i++)
            {
                WriteParticipant(_writer, _participants[i]);
            }

            if (!_backend.Send(peer.Connection, _writer.Written, DeliveryMode.Reliable))
            {
                // Nobody else heard of this player yet, so taking them back out tells no one.
                _participants.RemoveAt(_participants.Count - 1);
                _peers.Remove(peer.Connection);
                _backend.Disconnect(peer.Connection, ReasonCodes.ToWire(MultiplayerReason.ConnectionLost));
                return;
            }

            peer.IsWelcomed = true;

            _writer.Begin(PacketKind.ParticipantJoined);
            WriteParticipant(_writer, joined);
            BroadcastToWelcomed(peer.Connection);

            Raise(OnParticipantJoined, joined);
        }

        private void HostApplyReady(PlayerId player, bool isReady)
        {
            if (_state != SessionState.Lobby)
            {
                return;
            }

            int index = IndexOfParticipant(player);
            if (index < 0 || _participants[index].IsReady == isReady)
            {
                return;
            }

            Participant updated = _participants[index].WithReady(isReady);
            _participants[index] = updated;

            _writer.Begin(PacketKind.ParticipantUpdated);
            _writer.WriteULong(updated.Id.Value);
            _writer.WriteBool(updated.IsReady);
            _writer.WriteBool(updated.IsLateJoiner);
            BroadcastToWelcomed(ConnectionId.None);

            Raise(OnParticipantChanged, updated);
        }

        private void RemoveParticipant(PlayerId player, MultiplayerReason reason)
        {
            int index = IndexOfParticipant(player);
            if (index < 0)
            {
                return;
            }

            Participant removed = _participants[index];
            _participants.RemoveAt(index);

            _writer.Begin(PacketKind.ParticipantLeft);
            _writer.WriteULong(player.Value);
            _writer.WriteInt((int)reason);
            BroadcastToWelcomed(ConnectionId.None);

            Raise(OnParticipantLeft, removed, reason);
        }

        /// <summary>
        /// Sends what the writer holds to every accepted player except <paramref name="except"/>.
        /// A player the packet cannot reach is dropped from the session, since from then on its
        /// view of the session would silently differ from everyone else's.
        /// </summary>
        private void BroadcastToWelcomed(ConnectionId except)
        {
            List<PeerConnection> unreachable = null;

            ReadOnlySpan<byte> packet = _writer.Written;
            foreach (PeerConnection peer in _peers.Values)
            {
                if (peer.IsWelcomed && peer.Connection != except && !_backend.Send(peer.Connection, packet, DeliveryMode.Reliable))
                {
                    unreachable ??= new List<PeerConnection>();
                    unreachable.Add(peer);
                }
            }

            if (unreachable == null)
            {
                return;
            }

            for (int i = 0; i < unreachable.Count; i++)
            {
                PeerConnection peer = unreachable[i];
                if (_peers.Remove(peer.Connection))
                {
                    _backend.Disconnect(peer.Connection, ReasonCodes.ToWire(MultiplayerReason.ConnectionLost));
                    RemoveParticipant(peer.Player, MultiplayerReason.ConnectionLost);
                }
            }
        }

        /// <summary>
        /// The player's name as the backend reports it, cut to <see cref="MultiplayerLimits.MaxPlayerNameLength"/>
        /// so the roster always fits in one packet.
        /// </summary>
        private string GetParticipantName(PlayerId player)
        {
            string name = _backend.GetPlayerName(player) ?? string.Empty;
            return name.Length <= MultiplayerLimits.MaxPlayerNameLength
                ? name
                : name.Substring(0, MultiplayerLimits.MaxPlayerNameLength);
        }

        private PeerConnection FindWelcomedPeer(PlayerId player)
        {
            foreach (PeerConnection peer in _peers.Values)
            {
                if (peer.IsWelcomed && peer.Player == player)
                {
                    return peer;
                }
            }

            return null;
        }

        private void CloseAllPeers(MultiplayerReason reason)
        {
            int code = ReasonCodes.ToWire(reason);
            foreach (PeerConnection peer in _peers.Values)
            {
                _backend.Disconnect(peer.Connection, code);
            }

            _peers.Clear();
        }

        private void UpdateHandshakeTimeouts()
        {
            if (!_isHost || _peers.Count == 0)
            {
                return;
            }

            double now = Now;
            foreach (PeerConnection peer in _peers.Values)
            {
                if (!peer.IsWelcomed && now >= peer.HandshakeDeadline)
                {
                    _expiredPeers.Add(peer);
                }
            }

            for (int i = 0; i < _expiredPeers.Count; i++)
            {
                PeerConnection peer = _expiredPeers[i];
                _peers.Remove(peer.Connection);
                _backend.Disconnect(peer.Connection, ReasonCodes.ToWire(MultiplayerReason.Timeout));
            }

            _expiredPeers.Clear();
        }

        private void ResetHostState()
        {
            _peers.Clear();
            _expiredPeers.Clear();
            _bannedPlayers.Clear();
        }
        #endregion
    }
}
