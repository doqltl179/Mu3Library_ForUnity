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

        private double _joinDeadline = 0d;



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
                EndSession(reason, raiseEvents: true);
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
                // Tried again after the retry interval, while the reservation lasts.
                _backend.Disconnect(connection, ReasonCodes.ToWire(MultiplayerReason.ConnectionLost));
                _hostConnection = ConnectionId.None;
            }
            else
            {
                EndSession(MultiplayerReason.ConnectionFailed, raiseEvents: true);
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
                reason = sent != MultiplayerReason.None ? sent : MultiplayerReason.ConnectionFailed;
            }
            else if (_state == SessionState.Reconnecting)
            {
                // An attempt that did not get through is tried again; a host that answered
                // decided. It refuses a player whose slot is gone with ConnectionLost.
                if (kind != BackendDisconnectKind.ClosedByPeer)
                {
                    return;
                }

                reason = sent == MultiplayerReason.Banned || sent == MultiplayerReason.Kicked || sent == MultiplayerReason.HostLeft
                    ? sent
                    : MultiplayerReason.ConnectionLost;
            }
            else if (kind == BackendDisconnectKind.ConnectionLost || sent == MultiplayerReason.ConnectionLost)
            {
                // The connection broke, or the host closed it because its messages could not
                // reach this player; in a game the host keeps the slot for a while. Outside a
                // game, a connection that broke means the host is gone.
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
                    Debug.LogWarning($"[Multiplayer] Dropped {kind} from the host before its welcome.");
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
                    Raise(OnRulesChanged, _rules);
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
                    Debug.LogWarning($"[Multiplayer] Dropped {kind} from the host {remote}.");
                    break;
            }
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
            byte[] startData = reader.ReadBytes();
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
                _lobbyData.Add(pair.Key, pair.Value);
            }

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

            Action<MultiplayerResult> callback = _pendingCallback;
            _pendingCallback = null;
            Invoke(callback, MultiplayerResult.Success);

            // A late joiner arrives in a running game and needs its start data as much as anyone.
            if (state == SessionState.InGame && generation == _sessionGeneration)
            {
                Raise(OnGameStarted, new ReadOnlyMemory<byte>(_startData));
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
            Raise(OnParticipantJoined, joined);
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
            Raise(OnParticipantLeft, removed, reason);
        }

        private void HandleParticipantUpdated(ref PacketReader reader)
        {
            PlayerId player = new PlayerId(reader.ReadULong());
            bool isReady = reader.ReadBool();
            bool isLateJoiner = reader.ReadBool();

            int index = IndexOfParticipant(player);
            if (index < 0)
            {
                return;
            }

            Participant updated = _participants[index].WithReady(isReady).WithLateJoiner(isLateJoiner);
            _participants[index] = updated;
            Raise(OnParticipantChanged, updated);
        }

        private void HandleGameStarted(ref PacketReader reader)
        {
            int gameNumber = reader.ReadInt();
            byte[] startData = reader.ReadBytes();
            if (_state != SessionState.Lobby)
            {
                return;
            }

            _gameNumber = gameNumber;
            _startData = startData;

            int generation = _sessionGeneration;
            SetState(SessionState.InGame, MultiplayerReason.None);
            if (generation == _sessionGeneration)
            {
                Raise(OnGameStarted, new ReadOnlyMemory<byte>(_startData));
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
                Raise(OnReturnedToLobby);
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
            _joinDeadline = 0d;
        }
        #endregion
    }
}
