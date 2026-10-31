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

            /// <summary>True once the player's hello passed the version check; the player may still wait for lobby membership.</summary>
            public bool HasSaidHello;

            /// <summary>True once the host accepted the player's hello and added them to the roster.</summary>
            public bool IsWelcomed;

            /// <summary>True when the player's hello says it comes back to the slot the host keeps for it.</summary>
            public bool IsReturning;

            /// <summary>True while the player's hello waits for room in the rate limits, with its size.</summary>
            public bool IsHelloDelayed;
            public int HelloBytes;

            public double HandshakeDeadline;
        }

        private readonly Dictionary<ConnectionId, PeerConnection> _peers = new Dictionary<ConnectionId, PeerConnection>();
        private readonly List<PeerConnection> _expiredPeers = new List<PeerConnection>();
        private readonly List<PeerConnection> _waitingPeers = new List<PeerConnection>();
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
            foreach (KeyValuePair<string, string> pair in options.Data)
            {
                _lobbyData[pair.Key] = pair.Value;
            }

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
            data[LobbyDataKeys.WireVersion] = WireProtocol.VersionText;
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

            // Checked entry by entry, as SetLobbyData later checks each change.
            Dictionary<string, string> accepted = new Dictionary<string, string>(options.Data.Count);
            foreach (KeyValuePair<string, string> pair in options.Data)
            {
                MultiplayerReason check = CheckLobbyKeyCase(pair.Key, accepted);
                if (check != MultiplayerReason.None)
                {
                    return check;
                }

                check = CheckDataEntry(pair.Key, pair.Value, accepted, MultiplayerLimits.MaxLobbyDataSize);
                WarnIfInvalid(check, pair.Key);
                if (check != MultiplayerReason.None)
                {
                    return check;
                }

                accepted.Add(pair.Key, pair.Value);
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

            if (rules.MaxMessagesPerSecond < 0 || rules.MaxBytesPerSecond < 0
                || !IsValidSeconds(rules.RateLimitKickSeconds) || !IsValidSeconds(rules.ReconnectReservationSeconds)
                || !IsValidSeconds(rules.LoadTimeoutSeconds))
            {
                return MultiplayerReason.InvalidArgument;
            }

            return MultiplayerReason.None;
        }

        /// <summary>A length of time in the rules: finite, and 0 or more.</summary>
        private static bool IsValidSeconds(float seconds) => seconds >= 0f && !float.IsInfinity(seconds);
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
            return SendToHost(DeliveryMode.Reliable)
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

            if (rules.ReconnectReservationSeconds < _rules.ReconnectReservationSeconds)
            {
                ShortenReservations(rules.ReconnectReservationSeconds);
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

            KickPlayer(player, MultiplayerReason.Kicked);
            return MultiplayerResult.Success;
        }

        /// <summary>
        /// Bans the player for the rest of the session, closes their connection with the reason,
        /// and tells everyone they left for it.
        /// </summary>
        private void KickPlayer(PlayerId player, MultiplayerReason reason)
        {
            _bannedPlayers.Add(player);
            DisconnectPlayer(player, reason);
        }

        /// <summary>Closes the player's connection with the reason and tells everyone they left for it.</summary>
        private void DisconnectPlayer(PlayerId player, MultiplayerReason reason)
        {
            PeerConnection peer = FindWelcomedPeer(player);
            if (peer != null)
            {
                _peers.Remove(peer.Connection);
                _backend.Disconnect(peer.Connection, ReasonCodes.ToWire(reason));
            }

            RemoveParticipant(player, reason);
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

            // A returned player still entering the lobby again keeps it open until it is in.
            if (!_rules.AllowLateJoin && _lobbyRejoins.Count == 0)
            {
                _backend.SetLobbyJoinable(_lobbyId, false);
            }

            _gameNumber++;
            BeginLoadWait();
            _writer.Begin(PacketKind.GameStarted).WriteInt(_gameNumber);
            _writer.WriteBytes(_startData);
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
            EndLoadWait();

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
                _participants[i] = _participants[i].WithReady(false).WithLateJoiner(false).WithLoaded(false);
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

            // Each waits for the handshake timeout, so an account that keeps opening connections
            // without saying hello could otherwise fill the host's list.
            if (CountPendingConnections(remote) >= MultiplayerLimits.MaxPendingConnectionsPerAccount)
            {
                if (IsFirstPacketDrop(default, PacketDrop.ExtraConnection))
                {
                    Debug.LogWarning($"[Multiplayer] Closed a connection from {remote}: {MultiplayerLimits.MaxPendingConnectionsPerAccount} of its connections already wait for the welcome. Further such connections close unlogged until the session ends.");
                }

                _backend.Disconnect(connection, ReasonCodes.ToWire(MultiplayerReason.Timeout));
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

            // A client can only leave on its own; any other reason it names would let it show the
            // others something the host never decided, such as a kick.
            bool hasLeft = kind == BackendDisconnectKind.ClosedByPeer
                && ReasonCodes.FromWire(reasonCode) == MultiplayerReason.LeftNormally;

            if (!peer.IsWelcomed)
            {
                // A player who leaves on the way back to the slot the host keeps frees it at once,
                // as leaving always does.
                if (hasLeft && _reservations.ContainsKey(peer.Player))
                {
                    RemoveParticipant(peer.Player, MultiplayerReason.LeftNormally);
                }

                return;
            }

            if (hasLeft)
            {
                RemoveParticipant(peer.Player, MultiplayerReason.LeftNormally);
            }
            else
            {
                LosePlayer(peer.Player);
            }
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
                    if (IsFirstPacketDrop(kind, PacketDrop.BeforeHandshake))
                    {
                        Debug.LogWarning($"[Multiplayer] Dropped {kind} from {remote} before its hello. Further {PacketsOf(kind)} before a hello go unlogged until the session ends.");
                    }

                    return;
                }

                int wireVersion = reader.ReadInt();
                string gameId = reader.ReadString(MaxGameIdBytes);
                int protocolVersion = reader.ReadInt();
                HandleHello(peer, gameId, protocolVersion, wireVersion, data.Length, ref reader);
                return;
            }

            switch (CheckRateLimit(peer.Player, kind, data.Length))
            {
                case RateDecision.Drop:
                case RateDecision.Kicked:
                    return;

                case RateDecision.Delay:
                    DelayRequest(peer.Player, kind, data.Length, ref reader);
                    return;
            }

            switch (kind)
            {
                case PacketKind.SetReady:
                    HostApplyReady(peer.Player, reader.ReadBool());
                    break;

                case PacketKind.MessageToHost:
                    HostHandleMessage(peer, ref reader);
                    break;

                case PacketKind.SetParticipantData:
                    HostHandleParticipantData(peer, ref reader);
                    break;

                case PacketKind.DeleteParticipantData:
                    HostHandleParticipantDataDeletion(peer, ref reader);
                    break;

                case PacketKind.ChatToHost:
                    HostHandleChat(peer, ref reader);
                    break;

                case PacketKind.ReportLoaded:
                    HostHandleLoaded(peer, ref reader);
                    break;

                default:
                    // Only the host sends the other kinds; a client that sends one is ignored.
                    if (IsFirstPacketDrop(kind, PacketDrop.WrongDirection))
                    {
                        Debug.LogWarning($"[Multiplayer] Dropped {kind} from client {remote}. Further {PacketsOf(kind)} from clients go unlogged until the session ends.");
                    }

                    break;
            }
        }

        private void HandleHello(PeerConnection peer, string gameId, int protocolVersion, int wireVersion, int bytes, ref PacketReader reader)
        {
            if (!string.Equals(gameId, _gameId, StringComparison.Ordinal)
                || protocolVersion != _protocolVersion
                || wireVersion != WireProtocol.Version)
            {
                RefusePeer(peer, MultiplayerReason.VersionMismatch);
                return;
            }

            // Read only once the version matches, so a hello of any version is refused.
            peer.IsReturning = reader.ReadBool();

            // A kicked account gets nowhere, whatever else its hello would wait for.
            if (_bannedPlayers.Contains(peer.Player))
            {
                RefusePeer(peer, MultiplayerReason.Banned);
                return;
            }

            // An account the session heard from before counts its hellos as well, so connecting
            // again starts no fresh window; a hello past a limit waits for room, up to the
            // handshake timeout.
            if (_rates.ContainsKey(peer.Player))
            {
                RateDecision decision = CheckRateLimit(peer.Player, PacketKind.Hello, bytes);
                if (decision == RateDecision.Kicked)
                {
                    if (_peers.ContainsKey(peer.Connection) && _bannedPlayers.Contains(peer.Player))
                    {
                        RefusePeer(peer, MultiplayerReason.Banned);
                    }

                    return;
                }

                if (decision != RateDecision.Handle)
                {
                    peer.IsHelloDelayed = true;
                    peer.HelloBytes = bytes;
                    return;
                }
            }

            peer.HasSaidHello = true;
            TryAdmit(peer);
        }

        private void RefusePeer(PeerConnection peer, MultiplayerReason reason)
        {
            _peers.Remove(peer.Connection);
            _backend.Disconnect(peer.Connection, ReasonCodes.ToWire(reason));
        }

        /// <summary>
        /// Admits a player who said hello, once the lobby lists them as a member. Anyone who knows
        /// the host's account can open a connection, so membership is what keeps an invite-only or
        /// friends-only session to the players the lobby let in. The lobby may list a new member a
        /// moment after the connection opens, so a player who is not listed yet waits, up to the
        /// handshake timeout. An account whose slot the host keeps goes back to it instead.
        /// </summary>
        private void TryAdmit(PeerConnection peer)
        {
            if (TryReturnToSlot(peer))
            {
                return;
            }

            if (!_backend.IsLobbyMember(_lobbyId, peer.Player))
            {
                return;
            }

            MultiplayerReason reject = MultiplayerReason.None;
            if (_bannedPlayers.Contains(peer.Player))
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
                RefusePeer(peer, reject);
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

            if (!SendWelcome(peer))
            {
                // Nobody else heard of this player yet, so taking them back out tells no one.
                _participants.RemoveAt(_participants.Count - 1);
                RefusePeer(peer, MultiplayerReason.ConnectionLost);
                return;
            }

            peer.IsWelcomed = true;

            _writer.Begin(PacketKind.ParticipantJoined);
            WriteParticipant(_writer, joined);
            BroadcastToWelcomed(peer.Connection);

            Raise(OnParticipantJoined, joined);
        }

        /// <summary>Sends the player the session as it stands: state, rules, data, and the roster.</summary>
        private bool SendWelcome(PeerConnection peer)
        {
            _writer.Begin(PacketKind.Welcome);
            _writer.WriteByte((byte)_state);
            _writer.WriteULong(_hostId.Value);
            _writer.WriteInt(_gameNumber);
            WriteRules(_writer, _rules);
            _writer.WriteInt(_maxMessageSize);
            _writer.WriteBytes(_startData);
            WriteData(_writer, _lobbyData);
            _writer.WriteInt(_participants.Count);
            for (int i = 0; i < _participants.Count; i++)
            {
                WriteParticipant(_writer, _participants[i]);
            }

            return _backend.Send(peer.Connection, _writer.Written, DeliveryMode.Reliable);
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

            WriteParticipantUpdated(updated);
            BroadcastToWelcomed(ConnectionId.None);

            Raise(OnParticipantChanged, updated);
        }

        private void WriteParticipantUpdated(Participant participant)
        {
            _writer.Begin(PacketKind.ParticipantUpdated);
            _writer.WriteULong(participant.Id.Value);
            _writer.WriteBool(participant.IsReady);
            _writer.WriteBool(participant.IsLateJoiner);
            _writer.WriteBool(participant.IsLoaded);
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
            _reservations.Remove(player);
            DiscardDelayedRequests(player);

            _writer.Begin(PacketKind.ParticipantLeft);
            _writer.WriteULong(player.Value);
            _writer.WriteInt((int)reason);
            BroadcastToWelcomed(ConnectionId.None);

            int generation = _sessionGeneration;
            Raise(OnParticipantLeft, removed, reason);
            if (generation == _sessionGeneration)
            {
                // A player who left is no longer one the game started with, even if it joins again late.
                _loadStartedWith.Remove(player);
                StopWaitingFor(player);
            }
        }

        /// <summary>
        /// Sends what the writer holds to every accepted player except <paramref name="except"/>.
        /// A player the packet cannot reach is dropped from the session, since from then on its
        /// view of the session would silently differ from everyone else's.
        /// </summary>
        private void BroadcastToWelcomed(ConnectionId except)
        {
            foreach (PeerConnection peer in _peers.Values)
            {
                if (peer.IsWelcomed && peer.Connection != except)
                {
                    SendToPeer(peer, DeliveryMode.Reliable);
                }
            }

            DropUnreachablePeers();
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

        /// <summary>The connections of the account that the host has not welcomed yet.</summary>
        private int CountPendingConnections(PlayerId player)
        {
            int count = 0;
            foreach (PeerConnection peer in _peers.Values)
            {
                if (!peer.IsWelcomed && peer.Player == player)
                {
                    count++;
                }
            }

            return count;
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

            // Players who said hello before the lobby listed them, or whose hello waits for room
            // in the rate limits, get another look each update.
            foreach (PeerConnection peer in _peers.Values)
            {
                if ((peer.HasSaidHello || peer.IsHelloDelayed) && !peer.IsWelcomed)
                {
                    _waitingPeers.Add(peer);
                }
            }

            int generation = _sessionGeneration;
            for (int i = 0; i < _waitingPeers.Count && generation == _sessionGeneration; i++)
            {
                PeerConnection waiting = _waitingPeers[i];
                if (!_peers.ContainsKey(waiting.Connection))
                {
                    continue;
                }

                if (waiting.IsHelloDelayed)
                {
                    TakeDelayedHello(waiting);
                }
                else
                {
                    TryAdmit(waiting);
                }
            }

            _waitingPeers.Clear();
            if (generation != _sessionGeneration)
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
            _waitingPeers.Clear();
            _peers.Clear();
            _expiredPeers.Clear();
            _bannedPlayers.Clear();
            _reservations.Clear();
            _expiredReservations.Clear();
            _shortenedReservations.Clear();
            ResetRateState();
        }
        #endregion
    }
}
