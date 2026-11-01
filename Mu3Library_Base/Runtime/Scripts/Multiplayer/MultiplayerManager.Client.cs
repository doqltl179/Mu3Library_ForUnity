using System;
using System.Collections.Generic;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Protocol;
using UnityEngine;

namespace Mu3Library.Multiplayer
{
    public partial class MultiplayerManager
    {
        // The lobby the current JoinLobby asked for, before the lobby answers and it becomes current.
        private LobbyId _joiningLobbyId = LobbyId.None;

        private ConnectionId _hostConnection = ConnectionId.None;

        // True once the host accepted this player; until then the only packet that counts is Welcome.
        private bool _isWelcomed = false;

        // True while this player joins through the host after the lobby refused it, as a player
        // whose slot the host keeps may (FR-21), so it enters the lobby once the host takes it.
        private bool _isJoiningPastLobby = false;

        private double _joinDeadline = 0d;

        // How long a client waits for its host's connection to close once the lobby says the host
        // left, so the host's last messages are not dropped when the lobby's news overtakes them.
        private const double HostCloseWaitSeconds = 1.0;

        // True while the lobby says the host left and the host's connection is still open, until when.
        private bool _isAwaitingHostClose = false;
        private double _hostCloseDeadline = 0d;



        #region Join
        public void JoinLobby(LobbyId lobby, Action<MultiplayerResult> callback = null)
        {
            MultiplayerReason check = CheckCanStartSession();
            if (check == MultiplayerReason.None && !lobby.IsValid)
            {
                check = MultiplayerReason.InvalidArgument;
            }

            if (check != MultiplayerReason.None)
            {
                Invoke(callback, MultiplayerResult.Failure(check));
                return;
            }

            _isHost = false;
            _pendingCallback = callback;
            _joiningLobbyId = lobby;
            _joinDeadline = Now + _joinTimeoutSeconds;

            int generation = _sessionGeneration;
            SetState(SessionState.Joining, MultiplayerReason.None);
            if (generation != _sessionGeneration)
            {
                return;
            }

            _backend.JoinLobby(lobby, reason => HandleLobbyJoined(generation, lobby, reason));
        }

        private void HandleLobbyJoined(int generation, LobbyId lobby, MultiplayerReason reason)
        {
            if (generation != _sessionGeneration || _state != SessionState.Joining)
            {
                // The join was abandoned while the lobby answered; do not stay in a lobby nobody
                // plays in, unless a newer session of this player is in that same lobby.
                if (reason == MultiplayerReason.None && !IsInSessionLobby(lobby))
                {
                    _backend.LeaveLobby(lobby);
                }

                return;
            }

            if (reason != MultiplayerReason.None)
            {
                if (reason != MultiplayerReason.GameInProgress || !TryJoinPastLobby(lobby))
                {
                    EndSession(reason, raiseEvents: true);
                }

                return;
            }

            _lobbyId = lobby;

            // The lobby data answers before any connection exists, so a version mismatch never
            // reaches the host.
            string gameId = _backend.GetLobbyData(lobby, LobbyDataKeys.GameId);
            string protocolVersion = _backend.GetLobbyData(lobby, LobbyDataKeys.ProtocolVersion);
            string wireVersion = _backend.GetLobbyData(lobby, LobbyDataKeys.WireVersion);
            if (!IsCompatible(gameId, protocolVersion, wireVersion))
            {
                EndSession(MultiplayerReason.VersionMismatch, raiseEvents: true);
                return;
            }

            PlayerId host = ParsePlayerId(_backend.GetLobbyData(lobby, LobbyDataKeys.HostId));
            PlayerId owner = _backend.GetLobbyOwner(lobby);
            if (!host.IsValid || host == LocalPlayerId || (owner.IsValid && owner != host))
            {
                // Without its host the lobby no longer runs a session, even if members remain.
                EndSession(MultiplayerReason.LobbyNotFound, raiseEvents: true);
                return;
            }

            _hostId = host;
            _hostConnection = _backend.Connect(host);
            if (!_hostConnection.IsValid)
            {
                EndSession(MultiplayerReason.ConnectionFailed, raiseEvents: true);
            }
        }

        /// <summary>
        /// The lobby refused this player with <see cref="MultiplayerReason.GameInProgress"/>, and
        /// its data says that its game refuses late joiners (<see cref="LobbyDataKeys.Closed"/>).
        /// This player may still hold a slot in that game, as when it starts the game again within
        /// the reservation time after a crash (FR-21), and only the host knows. So it connects to
        /// the host the lobby data names, without entering the lobby: the host takes it back into
        /// its slot and opens the lobby for it, inviting it to a lobby that is not public, as for
        /// any player back in its slot (FR-20), and refuses every other account with
        /// <see cref="MultiplayerReason.GameInProgress"/>. Steam answers the same reason for an
        /// invite-only or friends-only lobby whose game takes late joiners, when this player has no
        /// invite; such a game's host would wait for a membership that never comes, so the
        /// refusal stands there. False, and the refusal stands, also when the lobby data, which a
        /// backend lets a player read after such a refusal, names no host of a compatible session,
        /// or names another player than the lobby's owner, when the owner is known.
        /// </summary>
        private bool TryJoinPastLobby(LobbyId lobby)
        {
            if (_backend.GetLobbyData(lobby, LobbyDataKeys.Closed) != LobbyDataKeys.ClosedForTheGame)
            {
                return false;
            }

            string gameId = _backend.GetLobbyData(lobby, LobbyDataKeys.GameId);
            string protocolVersion = _backend.GetLobbyData(lobby, LobbyDataKeys.ProtocolVersion);
            string wireVersion = _backend.GetLobbyData(lobby, LobbyDataKeys.WireVersion);
            PlayerId host = ParsePlayerId(_backend.GetLobbyData(lobby, LobbyDataKeys.HostId));
            PlayerId owner = _backend.GetLobbyOwner(lobby);
            if (!IsCompatible(gameId, protocolVersion, wireVersion) || !host.IsValid || host == LocalPlayerId
                || (owner.IsValid && owner != host))
            {
                return false;
            }

            ConnectionId connection = _backend.Connect(host);
            if (!connection.IsValid)
            {
                return false;
            }

            _lobbyId = lobby;
            _hostId = host;
            _hostConnection = connection;
            _isJoiningPastLobby = true;
            return true;
        }

        private void UpdateJoinTimeout()
        {
            if (_state == SessionState.Joining && !_isHost && Now >= _joinDeadline)
            {
                EndSession(MultiplayerReason.Timeout, raiseEvents: true);
            }
        }
        #endregion

        #region Client Connection
        private void ClientHandleConnected(ConnectionId connection)
        {
            bool isReturning = _state == SessionState.Reconnecting;
            if (_isHost || connection != _hostConnection || (_state != SessionState.Joining && !isReturning))
            {
                return;
            }

            _writer.Begin(PacketKind.Hello);
            _writer.WriteInt(WireProtocol.Version);
            _writer.WriteString(_gameId);
            _writer.WriteInt(_protocolVersion);
            _writer.WriteBool(isReturning);
            if (_backend.Send(connection, _writer.Written, DeliveryMode.Reliable))
            {
                return;
            }

            if (isReturning)
            {
                // Tried again after the retry interval, while the time to try lasts.
                _backend.Disconnect(connection, ReasonCodes.ToWire(MultiplayerReason.ConnectionLost));
                _hostConnection = ConnectionId.None;
            }
            else
            {
                EndSession(JoinConnectionFailure, raiseEvents: true);
            }
        }

        /// <summary>
        /// Why a join ends when the connection to the host fails without a reason from the host:
        /// <see cref="MultiplayerReason.ConnectionFailed"/>, or the lobby's own refusal for a
        /// player that tried the host past the lobby, since the host may well have refused it.
        /// </summary>
        private MultiplayerReason JoinConnectionFailure => _isJoiningPastLobby ? MultiplayerReason.GameInProgress : MultiplayerReason.ConnectionFailed;

        /// <summary>
        /// The lobby says the host left, while its connection to this player is still open. A
        /// host that leaves closes its connections before it leaves its lobby, but the lobby's
        /// news travels apart from them and may arrive before the messages the host sent last: on
        /// Steam the lobby and the connections are separate services, and on the machine-wide
        /// local network what the socket did not take at once follows in the background. So the
        /// session waits up to <see cref="HostCloseWaitSeconds"/> for the close, which ends it
        /// with <see cref="MultiplayerReason.HostLeft"/> after every message before it, and ends
        /// the same way without it, well inside the 2 seconds NFR-4 allows.
        /// </summary>
        private void AwaitHostClose()
        {
            if (!_isAwaitingHostClose)
            {
                _isAwaitingHostClose = true;
                _hostCloseDeadline = Now + HostCloseWaitSeconds;
            }
        }

        private void UpdateHostClose()
        {
            if (_isAwaitingHostClose && Now >= _hostCloseDeadline)
            {
                EndSession(MultiplayerReason.HostLeft, raiseEvents: true);
            }
        }

        private void ClientHandleDisconnected(ConnectionId connection, BackendDisconnectKind kind, int reasonCode)
        {
            if (!connection.IsValid || connection != _hostConnection)
            {
                return;
            }

            // The connection is already gone; ending the session must not close it again.
            _hostConnection = ConnectionId.None;

            MultiplayerReason sent = kind == BackendDisconnectKind.ClosedByPeer
                ? ReasonCodes.FromWire(reasonCode)
                : MultiplayerReason.None;

            MultiplayerReason reason;
            if (_state == SessionState.Joining)
            {
                // A refusal names its reason: full, banned, in progress, version, or duplicate.
                // Past the lobby, a host that answers Timeout closed a player that waited for a
                // membership, which leaves the lobby's refusal standing too.
                reason = sent == MultiplayerReason.None || (_isJoiningPastLobby && sent == MultiplayerReason.Timeout)
                    ? JoinConnectionFailure
                    : sent;
            }
            else if (_state == SessionState.Reconnecting)
            {
                // An attempt that did not get through, or that timed out before the host answered,
                // is tried again while the time to try lasts. A host that answered decided: a
                // player whose slot is gone hears ConnectionLost, unless the lobby shows the host
                // gone, and a host that left or runs no session any more means HostLeft.
                if (kind != BackendDisconnectKind.ClosedByPeer || sent == MultiplayerReason.None || sent == MultiplayerReason.Timeout)
                {
                    return;
                }

                if (sent == MultiplayerReason.Banned || sent == MultiplayerReason.Kicked || sent == MultiplayerReason.RateLimited)
                {
                    reason = sent;
                }
                else if (sent == MultiplayerReason.HostLeft || sent == MultiplayerReason.LobbyNotFound || IsHostGoneFromLobby())
                {
                    reason = MultiplayerReason.HostLeft;
                }
                else
                {
                    reason = MultiplayerReason.ConnectionLost;
                }
            }
            else if (kind == BackendDisconnectKind.ConnectionLost || sent == MultiplayerReason.ConnectionLost
                || (kind == BackendDisconnectKind.ClosedByPeer && sent == MultiplayerReason.None))
            {
                // The connection broke, the host closed it because its messages could not reach
                // this player, or it closed with a code that is not the library's, as Steam's own
                // are; in a game the host keeps the slot for a while. Outside a game, it means the
                // host is gone.
                LoseHost(sent == MultiplayerReason.ConnectionLost ? MultiplayerReason.ConnectionLost : MultiplayerReason.HostLeft);
                return;
            }
            else
            {
                // The host names why it closed this player's connection; anything else means the host is gone.
                reason = sent == MultiplayerReason.Kicked || sent == MultiplayerReason.RateLimited
                    ? sent
                    : MultiplayerReason.HostLeft;
            }

            EndSession(reason, raiseEvents: true);
        }

        private void ClientHandleReceived(ConnectionId connection, PlayerId remote, ReadOnlySpan<byte> data)
        {
            if (!connection.IsValid || connection != _hostConnection)
            {
                return;
            }

            PacketReader reader = new PacketReader(data);
            PacketKind kind = reader.ReadKind();

            if (!_isWelcomed)
            {
                if (kind != PacketKind.Welcome)
                {
                    if (!IsUnreliableFromHost(kind, data) && IsFirstPacketDrop(kind, PacketDrop.BeforeHandshake))
                    {
                        Debug.LogWarning($"[Multiplayer] Dropped {kind} from the host before its welcome. Further {PacketsOf(kind)} before the welcome go unlogged until the session ends.");
                    }

                    return;
                }

                HandleWelcome(ref reader);
                return;
            }

            switch (kind)
            {
                case PacketKind.ParticipantJoined:
                    HandleParticipantJoined(ref reader);
                    break;

                case PacketKind.ParticipantLeft:
                    HandleParticipantLeft(ref reader);
                    break;

                case PacketKind.ParticipantUpdated:
                    HandleParticipantUpdated(ref reader);
                    break;

                case PacketKind.RulesChanged:
                    _rules = ReadRules(ref reader);
                    Raise(_onRulesChanged, _rules);
                    break;

                case PacketKind.GameStarted:
                    HandleGameStarted(ref reader);
                    break;

                case PacketKind.ReturnedToLobby:
                    HandleReturnedToLobby();
                    break;

                case PacketKind.MessageFromHost:
                    ClientHandleMessage(ref reader);
                    break;

                case PacketKind.LobbyDataChanged:
                    HandleLobbyDataChanged(ref reader);
                    break;

                case PacketKind.ParticipantDataChanged:
                    HandleParticipantDataChanged(ref reader);
                    break;

                case PacketKind.LobbyDataDeleted:
                    HandleLobbyDataDeleted(ref reader);
                    break;

                case PacketKind.ParticipantDataDeleted:
                    HandleParticipantDataDeleted(ref reader);
                    break;

                case PacketKind.ParticipantPings:
                    HandleParticipantPings(ref reader);
                    break;

                case PacketKind.ChatFromHost:
                    HandleChatFromHost(ref reader);
                    break;

                case PacketKind.ParticipantDisconnected:
                    HandleParticipantConnection(ref reader, isConnected: false);
                    break;

                case PacketKind.ParticipantReconnected:
                    HandleParticipantConnection(ref reader, isConnected: true);
                    break;

                default:
                    if (IsFirstPacketDrop(kind, PacketDrop.WrongDirection))
                    {
                        Debug.LogWarning($"[Multiplayer] Dropped {kind} from the host {remote}. Further {PacketsOf(kind)} from the host go unlogged until the session ends.");
                    }

                    break;
            }
        }

        /// <summary>
        /// True for what a host sends unreliably: a ping report, and a game message whose delivery
        /// mode, the byte after its kind, says unreliable. Unreliable packets keep no order with
        /// the reliable welcome, so while a loss makes the network send the welcome again, the ping
        /// reports and unreliable messages that the host sent after it may arrive first. This
        /// player drops them, as everything before its welcome, but without a warning, so the
        /// warning names only a host that sent a reliable packet before welcoming the player, which
        /// the order of reliable packets rules out for a host that behaves.
        /// </summary>
        private static bool IsUnreliableFromHost(PacketKind kind, ReadOnlySpan<byte> data)
        {
            return kind == PacketKind.ParticipantPings
                || (kind == PacketKind.MessageFromHost && data.Length > 1 && data[1] == (byte)DeliveryMode.Unreliable);
        }

        private void HandleWelcome(ref PacketReader reader)
        {
            SessionState state = (SessionState)reader.ReadByte();
            if (state != SessionState.Lobby && state != SessionState.InGame)
            {
                throw new FormatException($"Invalid session state {state} in the welcome.");
            }

            PlayerId host = new PlayerId(reader.ReadULong());
            int gameNumber = reader.ReadInt();
            SessionRules rules = ReadRules(ref reader);
            int hostMaxMessageSize = reader.ReadInt();
            if (hostMaxMessageSize < 1)
            {
                throw new FormatException($"Invalid message size limit {hostMaxMessageSize} in the welcome.");
            }
            byte[] startData = reader.ReadBytes(MultiplayerLimits.MaxStartDataSize);
            Dictionary<string, string> lobbyData = ReadData(ref reader, MultiplayerLimits.MaxLobbyDataSize);

            int count = reader.ReadInt();
            if (count < 1 || count > MultiplayerLimits.MaxCapacity)
            {
                throw new FormatException($"Invalid participant count {count} in the welcome.");
            }

            List<Participant> roster = new List<Participant>(count);
            for (int i = 0; i < count; i++)
            {
                roster.Add(ReadParticipant(ref reader, host));
            }

            if (host != _hostId)
            {
                throw new FormatException($"The welcome names host {host}, but the lobby names {_hostId}.");
            }

            // A returning player that missed the end of its game, or that game and the start of
            // another, hears of it as it comes back.
            bool isReturning = _state == SessionState.Reconnecting;
            bool isSameGame = state == SessionState.InGame && gameNumber == _gameNumber;

            _isWelcomed = true;
            _gameNumber = gameNumber;
            _rules = rules;
            _hostMaxMessageSize = hostMaxMessageSize;
            _startData = startData;
            _participants.Clear();
            _participants.AddRange(roster);
            _lobbyData.Clear();
            foreach (KeyValuePair<string, string> pair in lobbyData)
            {
                _lobbyData[pair.Key] = pair.Value;
            }

            // A returning player waits for the next report rather than show the pings from before the drop.
            _reportedPings.Clear();

            if (TryGetParticipant(LocalPlayerId, out Participant self))
            {
                TakeRequestedData(self.Data);
            }

            // The callback stays pending through the state event: a handler that ends the session
            // there answers it with the reason, and only a session that survives answers success.
            int generation = _sessionGeneration;
            SetState(state, MultiplayerReason.None);
            if (generation != _sessionGeneration)
            {
                return;
            }

            if (isReturning)
            {
                HandleReturned(state, isSameGame);
                return;
            }

            if (_isJoiningPastLobby)
            {
                // Back in its slot from outside the lobby, which the host now opens for it.
                _isJoiningPastLobby = false;
                BeginLobbyRejoin();
            }

            Action<MultiplayerResult> callback = _pendingCallback;
            _pendingCallback = null;
            Invoke(callback, MultiplayerResult.Success);

            // A late joiner arrives in a running game and needs its start data as much as anyone.
            if (state == SessionState.InGame && generation == _sessionGeneration)
            {
                Raise(_onGameStarted, new ReadOnlyMemory<byte>(_startData));
            }
        }

        private void HandleParticipantJoined(ref PacketReader reader)
        {
            Participant joined = ReadParticipant(ref reader, _hostId);
            if (IndexOfParticipant(joined.Id) >= 0)
            {
                return;
            }

            _participants.Add(joined);
            Raise(_onParticipantJoined, joined);
        }

        private void HandleParticipantLeft(ref PacketReader reader)
        {
            PlayerId player = new PlayerId(reader.ReadULong());
            MultiplayerReason reason = (MultiplayerReason)reader.ReadInt();
            if (!Enum.IsDefined(typeof(MultiplayerReason), reason))
            {
                reason = MultiplayerReason.ConnectionLost;
            }

            int index = IndexOfParticipant(player);
            if (index < 0)
            {
                return;
            }

            Participant removed = _participants[index];
            _participants.RemoveAt(index);
            Raise(_onParticipantLeft, removed, reason);
        }

        private void HandleParticipantUpdated(ref PacketReader reader)
        {
            PlayerId player = new PlayerId(reader.ReadULong());
            bool isReady = reader.ReadBool();
            bool isLateJoiner = reader.ReadBool();
            bool isLoaded = reader.ReadBool();

            int index = IndexOfParticipant(player);
            if (index < 0)
            {
                return;
            }

            Participant updated = _participants[index].WithReady(isReady).WithLateJoiner(isLateJoiner).WithLoaded(isLoaded);
            _participants[index] = updated;
            Raise(_onParticipantChanged, updated);
        }

        private void HandleGameStarted(ref PacketReader reader)
        {
            int gameNumber = reader.ReadInt();
            byte[] startData = reader.ReadBytes(MultiplayerLimits.MaxStartDataSize);
            if (_state != SessionState.Lobby)
            {
                return;
            }

            _gameNumber = gameNumber;
            _startData = startData;

            // The host cleared every loaded flag as the game started.
            for (int i = 0; i < _participants.Count; i++)
            {
                _participants[i] = _participants[i].WithLoaded(false);
            }

            int generation = _sessionGeneration;
            SetState(SessionState.InGame, MultiplayerReason.None);
            if (generation == _sessionGeneration)
            {
                Raise(_onGameStarted, new ReadOnlyMemory<byte>(_startData));
            }
        }

        private void HandleReturnedToLobby()
        {
            if (_state != SessionState.InGame)
            {
                return;
            }

            ReturnRosterToLobby();

            int generation = _sessionGeneration;
            SetState(SessionState.Lobby, MultiplayerReason.None);
            if (generation == _sessionGeneration)
            {
                Raise(_onReturnedToLobby);
            }
        }

        private bool IsInSessionLobby(LobbyId lobby)
        {
            return _state != SessionState.Offline && (_lobbyId == lobby || _joiningLobbyId == lobby);
        }

        private void ResetClientState()
        {
            _joiningLobbyId = LobbyId.None;
            _hostConnection = ConnectionId.None;
            _isWelcomed = false;
            _isJoiningPastLobby = false;
            _joinDeadline = 0d;
            _isAwaitingHostClose = false;
            _hostCloseDeadline = 0d;
        }
        #endregion
    }
}
