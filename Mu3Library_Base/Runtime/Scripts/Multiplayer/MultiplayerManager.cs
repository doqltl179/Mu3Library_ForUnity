using System;
using System.Collections.Generic;
using System.Globalization;
using Mu3Library.DI;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Protocol;
using UnityEngine;

namespace Mu3Library.Multiplayer
{
    /// <summary>
    /// Runs a player-hosted session over an <see cref="IMultiplayerBackend"/>. The lobby only lets
    /// players find and enter a session; the host's connections carry the session itself, so the
    /// roster, ready flags, rules, start, and kicks follow the host and never the lobby data.
    /// </summary>
    public partial class MultiplayerManager : IMultiplayerManager, IMultiplayerManagerEventBus, IMultiplayerMessenger, IUpdatable, IDisposable
    {
        private IMultiplayerBackend _backend;
        private BackendListener _listener;

        // Copied from the config at Initialize, so a config changed afterwards cannot bypass the
        // checks Initialize made.
        private string _gameId;
        private int _protocolVersion;
        private string _protocolVersionText;
        private double _joinTimeoutSeconds;
        private double _handshakeTimeoutSeconds;
        private Func<double> _clock;

        private bool _isInitialized = false;
        public bool IsInitialized => _isInitialized;

        private bool _isDisposed = false;

        private SessionState _state = SessionState.Offline;
        public SessionState State => _state;

        private bool _isHost = false;
        public bool IsHost => _isHost;

        public PlayerId LocalPlayerId => _backend != null ? _backend.LocalPlayerId : PlayerId.None;

        private PlayerId _hostId = PlayerId.None;
        public PlayerId HostId => _hostId;

        private LobbyId _lobbyId = LobbyId.None;
        public LobbyId CurrentLobbyId => _lobbyId;

        private SessionRules _rules = SessionRules.Default;
        public SessionRules Rules => _rules;

        private readonly List<Participant> _participants = new List<Participant>();
        public IReadOnlyList<Participant> Participants => _participants;

        private byte[] _startData = Array.Empty<byte>();
        public ReadOnlyMemory<byte> StartData => _startData;

        // Moves on whenever a session ends, so a backend callback that belongs to an earlier
        // session, or code that ran after an event handler ended the session, can tell.
        private int _sessionGeneration = 0;

        // The callback of the CreateLobby or JoinLobby call still in flight. It is answered once,
        // with success or with the reason the session ended.
        private Action<MultiplayerResult> _pendingCallback = null;

        private readonly PacketWriter _writer = new PacketWriter();

        public event Action<SessionStateChange> OnStateChanged;
        public event Action<Participant> OnParticipantJoined;
        public event Action<Participant, MultiplayerReason> OnParticipantLeft;
        public event Action<Participant> OnParticipantChanged;
        public event Action<SessionRules> OnRulesChanged;
        public event Action<ReadOnlyMemory<byte>> OnGameStarted;
        public event Action OnReturnedToLobby;
        public event Action<LobbyId> OnJoinRequested;

        private double Now => _clock != null ? _clock() : Time.realtimeSinceStartupAsDouble;



        public MultiplayerResult Initialize(IMultiplayerBackend backend, MultiplayerConfig config)
        {
            if (_isDisposed || _isInitialized)
            {
                return MultiplayerResult.Failure(MultiplayerReason.InvalidState);
            }

            if (backend == null || config == null)
            {
                return MultiplayerResult.Failure(MultiplayerReason.InvalidArgument);
            }

            if (string.IsNullOrEmpty(config.GameId) || config.GameId.Length > MultiplayerConfig.MaxGameIdLength)
            {
                Debug.LogWarning($"[Multiplayer] The game ID must be 1 to {MultiplayerConfig.MaxGameIdLength} characters.");
                return MultiplayerResult.Failure(MultiplayerReason.InvalidArgument);
            }

            if (!(config.JoinTimeoutSeconds > 0f) || !(config.HandshakeTimeoutSeconds > 0f))
            {
                Debug.LogWarning("[Multiplayer] Timeouts must be greater than zero.");
                return MultiplayerResult.Failure(MultiplayerReason.InvalidArgument);
            }

            if (!backend.IsAvailable)
            {
                return MultiplayerResult.Failure(MultiplayerReason.BackendError);
            }

            int largestMessage = backend.MaxPacketSize - MessageHeader.ToHostSize;
            if (config.MaxMessageSize < 1 || config.MaxMessageSize > largestMessage)
            {
                Debug.LogWarning($"[Multiplayer] The message size limit must be 1 to {largestMessage} bytes on the {backend.Name} backend.");
                return MultiplayerResult.Failure(MultiplayerReason.InvalidArgument);
            }

            _backend = backend;
            _gameId = config.GameId;
            _protocolVersion = config.ProtocolVersion;
            _protocolVersionText = config.ProtocolVersion.ToString(CultureInfo.InvariantCulture);
            _joinTimeoutSeconds = config.JoinTimeoutSeconds;
            _handshakeTimeoutSeconds = config.HandshakeTimeoutSeconds;
            _maxMessageSize = config.MaxMessageSize;
            _clock = config.Clock;
            _isInitialized = true;

            _listener = new BackendListener(this);
            _backend.SetListener(_listener);

            return MultiplayerResult.Success;
        }

        public void Update()
        {
            if (!_isInitialized || _isDisposed)
            {
                return;
            }

            _backend.Update();

            UpdateHandshakeTimeouts();
            UpdateJoinTimeout();
        }

        /// <summary>
        /// Leaves any session without raising events, then lets go of the backend. The backend
        /// itself belongs to whoever created it.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            if (_isInitialized)
            {
                EndSession(MultiplayerReason.LeftNormally, raiseEvents: false);
                _backend.SetListener(null);
                _listener = null;
            }

            _isDisposed = true;

            OnStateChanged = null;
            OnParticipantJoined = null;
            OnParticipantLeft = null;
            OnParticipantChanged = null;
            OnRulesChanged = null;
            OnGameStarted = null;
            OnReturnedToLobby = null;
            OnJoinRequested = null;

            // Handlers often hold scene objects; a disposed manager must not keep them alive.
            _messages.Clear();
        }

        public bool TryGetParticipant(PlayerId player, out Participant participant)
        {
            int index = IndexOfParticipant(player);
            participant = index >= 0 ? _participants[index] : default;
            return index >= 0;
        }

        public void Leave()
        {
            if (!_isInitialized || _isDisposed)
            {
                return;
            }

            EndSession(MultiplayerReason.LeftNormally, raiseEvents: true);
        }

        #region Lobby Search
        public void SearchLobbies(LobbySearchQuery query, Action<LobbySearchResult> callback)
        {
            if (callback == null)
            {
                Debug.LogWarning("[Multiplayer] SearchLobbies needs a callback.");
                return;
            }

            if (!_isInitialized || _isDisposed)
            {
                Invoke(callback, new LobbySearchResult(MultiplayerReason.NotInitialized, null));
                return;
            }

            LobbySearchQuery effective = query != null ? query.Clone() : new LobbySearchQuery();
            effective.MaxResults = Mathf.Clamp(effective.MaxResults, 1, LobbySearchQuery.MaxResultLimit);
            effective.AddStringFilter(LobbyDataKeys.GameId, _gameId);
            effective.AddNumericFilter(LobbyDataKeys.ProtocolVersion, _protocolVersion);
            effective.AddNumericFilter(LobbyDataKeys.WireVersion, WireProtocol.Version);

            _backend.SearchLobbies(effective, (reason, lobbies) => HandleSearchCompleted(callback, reason, lobbies));
        }

        private void HandleSearchCompleted(Action<LobbySearchResult> callback, MultiplayerReason reason, IReadOnlyList<BackendLobby> lobbies)
        {
            if (_isDisposed)
            {
                return;
            }

            if (reason != MultiplayerReason.None || lobbies == null)
            {
                Invoke(callback, new LobbySearchResult(reason == MultiplayerReason.None ? MultiplayerReason.BackendError : reason, null));
                return;
            }

            List<LobbyInfo> results = new List<LobbyInfo>(lobbies.Count);
            for (int i = 0; i < lobbies.Count; i++)
            {
                BackendLobby lobby = lobbies[i];

                // The backend filters already; checking again keeps a backend that cannot filter
                // from offering another game's or another version's lobbies.
                if (!IsCompatibleLobby(lobby.Data))
                {
                    continue;
                }

                Dictionary<string, string> data = new Dictionary<string, string>();
                foreach (KeyValuePair<string, string> pair in lobby.Data)
                {
                    if (!LobbyDataKeys.IsReserved(pair.Key))
                    {
                        data[pair.Key] = pair.Value;
                    }
                }

                lobby.Data.TryGetValue(LobbyDataKeys.Name, out string name);
                lobby.Data.TryGetValue(LobbyDataKeys.HostId, out string host);
                lobby.Data.TryGetValue(LobbyDataKeys.State, out string state);

                results.Add(new LobbyInfo(
                    lobby.Id,
                    name,
                    ParsePlayerId(host),
                    lobby.MemberCount,
                    lobby.Capacity,
                    state == LobbyDataKeys.StateInGame,
                    data));
            }

            Invoke(callback, new LobbySearchResult(MultiplayerReason.None, results));
        }

        private bool IsCompatibleLobby(IReadOnlyDictionary<string, string> data)
        {
            if (data == null)
            {
                return false;
            }

            data.TryGetValue(LobbyDataKeys.GameId, out string gameId);
            data.TryGetValue(LobbyDataKeys.ProtocolVersion, out string protocol);
            data.TryGetValue(LobbyDataKeys.WireVersion, out string wire);
            return IsCompatible(gameId, protocol, wire);
        }

        /// <summary>
        /// Whether lobby data names this game, this game's protocol version, and this library's
        /// wire version. Builds that share a game protocol but not a library version cannot talk.
        /// </summary>
        private bool IsCompatible(string gameId, string protocolVersion, string wireVersion)
        {
            return string.Equals(gameId, _gameId, StringComparison.Ordinal)
                && string.Equals(protocolVersion, _protocolVersionText, StringComparison.Ordinal)
                && string.Equals(wireVersion, WireProtocol.VersionText, StringComparison.Ordinal);
        }
        #endregion

        #region Backend Listener
        /// <summary>
        /// Takes the backend's reports for the manager, so the manager's own type does not offer
        /// them to game code.
        /// </summary>
        private sealed class BackendListener : IMultiplayerBackendListener
        {
            private readonly MultiplayerManager _manager;

            public BackendListener(MultiplayerManager manager)
            {
                _manager = manager;
            }

            public void OnConnected(ConnectionId connection, PlayerId remote, bool isIncoming) => _manager.HandleBackendConnected(connection, remote, isIncoming);
            public void OnDisconnected(ConnectionId connection, PlayerId remote, BackendDisconnectKind kind, int reasonCode) => _manager.HandleBackendDisconnected(connection, kind, reasonCode);
            public void OnReceived(ConnectionId connection, PlayerId remote, ReadOnlySpan<byte> data) => _manager.HandleBackendReceived(connection, remote, data);
            public void OnLobbyOwnerChanged(LobbyId lobby, PlayerId newOwner) => _manager.HandleLobbyOwnerChanged(lobby, newOwner);
            public void OnLobbyJoinRequested(LobbyId lobby) => _manager.HandleLobbyJoinRequested(lobby);
        }

        private void HandleBackendConnected(ConnectionId connection, PlayerId remote, bool isIncoming)
        {
            if (_isDisposed)
            {
                return;
            }

            if (isIncoming)
            {
                HostHandleConnected(connection, remote);
            }
            else
            {
                ClientHandleConnected(connection);
            }
        }

        private void HandleBackendDisconnected(ConnectionId connection, BackendDisconnectKind kind, int reasonCode)
        {
            if (_isDisposed)
            {
                return;
            }

            if (_isHost)
            {
                HostHandleDisconnected(connection, kind, reasonCode);
            }
            else
            {
                ClientHandleDisconnected(connection, kind, reasonCode);
            }
        }

        private void HandleBackendReceived(ConnectionId connection, PlayerId remote, ReadOnlySpan<byte> data)
        {
            if (_isDisposed || data.IsEmpty)
            {
                return;
            }

            try
            {
                if (_isHost)
                {
                    HostHandleReceived(connection, remote, data);
                }
                else
                {
                    ClientHandleReceived(connection, remote, data);
                }
            }
            catch (FormatException e)
            {
                Debug.LogWarning($"[Multiplayer] Dropped a malformed packet from {remote}: {e.Message}");
            }
        }

        private void HandleLobbyOwnerChanged(LobbyId lobby, PlayerId newOwner)
        {
            if (_isDisposed || lobby != _lobbyId || !_lobbyId.IsValid)
            {
                return;
            }

            if (_isHost)
            {
                if (newOwner != LocalPlayerId)
                {
                    Debug.LogWarning($"[Multiplayer] The host no longer owns lobby {lobby}; its lobby data can no longer change.");
                }

                return;
            }

            // A lobby passes to another member when its owner leaves. The session lives on the
            // host's machine, so it ends with the host instead of passing on.
            if (newOwner != _hostId)
            {
                EndSession(MultiplayerReason.HostLeft, raiseEvents: true);
            }
        }

        private void HandleLobbyJoinRequested(LobbyId lobby)
        {
            if (_isDisposed || !lobby.IsValid || lobby == _lobbyId)
            {
                return;
            }

            int generation = _sessionGeneration;
            SessionState state = _state;
            Raise(OnJoinRequested, lobby);

            // A handler that already ended or started a session decided for itself.
            if (generation != _sessionGeneration || state != _state)
            {
                return;
            }

            if (_state != SessionState.Offline)
            {
                EndSession(MultiplayerReason.LeftNormally, raiseEvents: true);

                // So did a handler of that Offline transition that started another session.
                if (_state != SessionState.Offline)
                {
                    return;
                }
            }

            JoinLobby(lobby);
        }
        #endregion

        #region Session State
        private MultiplayerReason CheckCanStartSession()
        {
            if (!_isInitialized || _isDisposed)
            {
                return MultiplayerReason.NotInitialized;
            }

            return _state == SessionState.Offline ? MultiplayerReason.None : MultiplayerReason.InvalidState;
        }

        private void SetState(SessionState next, MultiplayerReason reason)
        {
            if (_state == next)
            {
                return;
            }

            SessionState previous = _state;
            _state = next;
            Raise(OnStateChanged, new SessionStateChange(previous, next, reason));
        }

        /// <summary>
        /// Ends the current session from any state: closes the connections, leaves the lobby,
        /// answers a pending create or join with the reason, and turns the state to Offline.
        /// </summary>
        private void EndSession(MultiplayerReason reason, bool raiseEvents)
        {
            if (_state == SessionState.Offline)
            {
                return;
            }

            if (_isHost)
            {
                CloseAllPeers(MultiplayerReason.HostLeft);
                _backend.StopListening();
            }
            else if (_hostConnection.IsValid)
            {
                _backend.Disconnect(_hostConnection, ReasonCodes.ToWire(MultiplayerReason.LeftNormally));
            }

            if (_lobbyId.IsValid)
            {
                _backend.LeaveLobby(_lobbyId);
            }

            Action<MultiplayerResult> callback = _pendingCallback;
            ResetSession();

            if (!raiseEvents)
            {
                _state = SessionState.Offline;
                return;
            }

            SetState(SessionState.Offline, reason);
            Invoke(callback, MultiplayerResult.Failure(reason));
        }

        private void ResetSession()
        {
            _sessionGeneration++;

            _isHost = false;
            _hostId = PlayerId.None;
            _lobbyId = LobbyId.None;
            _rules = SessionRules.Default;
            _startData = Array.Empty<byte>();
            _participants.Clear();
            _pendingCallback = null;

            ResetHostState();
            ResetClientState();
            ResetMessageState();
        }

        private int IndexOfParticipant(PlayerId player)
        {
            for (int i = 0; i < _participants.Count; i++)
            {
                if (_participants[i].Id == player)
                {
                    return i;
                }
            }

            return -1;
        }

        private static PlayerId ParsePlayerId(string text)
        {
            return ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong value)
                ? new PlayerId(value)
                : PlayerId.None;
        }
        #endregion

        #region Packets
        private static void WriteRules(PacketWriter writer, SessionRules rules)
        {
            writer.WriteInt(rules.Capacity);
            writer.WriteInt(rules.MinPlayersToStart);
            writer.WriteBool(rules.RequireReady);
            writer.WriteBool(rules.AllowLateJoin);
        }

        private static SessionRules ReadRules(ref PacketReader reader)
        {
            int capacity = reader.ReadInt();
            int minPlayersToStart = reader.ReadInt();
            bool requireReady = reader.ReadBool();
            bool allowLateJoin = reader.ReadBool();

            if (capacity < MultiplayerLimits.MinCapacity || capacity > MultiplayerLimits.MaxCapacity
                || minPlayersToStart < 1 || minPlayersToStart > capacity)
            {
                throw new FormatException($"Invalid rules: capacity {capacity}, minimum players {minPlayersToStart}.");
            }

            return new SessionRules(capacity, minPlayersToStart, requireReady, allowLateJoin);
        }

        private static void WriteParticipant(PacketWriter writer, Participant participant)
        {
            writer.WriteULong(participant.Id.Value);
            writer.WriteString(participant.Name);
            writer.WriteBool(participant.IsReady);
            writer.WriteBool(participant.IsLateJoiner);
        }

        private Participant ReadParticipant(ref PacketReader reader, PlayerId host)
        {
            PlayerId id = new PlayerId(reader.ReadULong());
            string name = reader.ReadString();
            bool isReady = reader.ReadBool();
            bool isLateJoiner = reader.ReadBool();

            if (!id.IsValid)
            {
                throw new FormatException("A participant has no player ID.");
            }

            return new Participant(id, name, id == host, id == LocalPlayerId, isReady, isLateJoiner);
        }
        #endregion

        #region Events
        private static void Raise(Action handler)
        {
            if (handler == null)
            {
                return;
            }

            try
            {
                handler();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void Raise<T>(Action<T> handler, T arg)
        {
            if (handler == null)
            {
                return;
            }

            try
            {
                handler(arg);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void Raise<T1, T2>(Action<T1, T2> handler, T1 arg1, T2 arg2)
        {
            if (handler == null)
            {
                return;
            }

            try
            {
                handler(arg1, arg2);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void Invoke<T>(Action<T> callback, T result) => Raise(callback, result);
        #endregion
    }
}
