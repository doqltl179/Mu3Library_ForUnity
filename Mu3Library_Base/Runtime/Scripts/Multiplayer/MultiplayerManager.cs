using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
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
        private double _reconnectTimeoutSeconds;
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

        // Counts the games of the current session, as the host numbered them; 0 before the first.
        // A returning player compares it to tell the game it left from the one it comes back to.
        private int _gameNumber = 0;

        // Moves on whenever a session ends, so a backend callback that belongs to an earlier
        // session, or code that ran after an event handler ended the session, can tell.
        private int _sessionGeneration = 0;

        // The callback of the CreateLobby or JoinLobby call still in flight. It is answered once,
        // with success or with the reason the session ended.
        private Action<MultiplayerResult> _pendingCallback = null;

        private readonly PacketWriter _writer = new PacketWriter();

        // Handler exceptions of the manager's events logged in this session, by event and
        // exception type, from the session's start to the next one's. The events are a fixed set,
        // and a handler's exception types come from the game's own code, so it needs no limit.
        private readonly HashSet<(string Event, Type Exception)> _loggedEventFailures = new HashSet<(string Event, Type Exception)>();

        // The handlers of each event, which Raise invokes one by one (see ManagerEvent).
        private readonly ManagerEvent<Action<SessionStateChange>> _onStateChanged = new ManagerEvent<Action<SessionStateChange>>(nameof(OnStateChanged));
        private readonly ManagerEvent<Action<Participant>> _onParticipantJoined = new ManagerEvent<Action<Participant>>(nameof(OnParticipantJoined));
        private readonly ManagerEvent<Action<Participant, MultiplayerReason>> _onParticipantLeft = new ManagerEvent<Action<Participant, MultiplayerReason>>(nameof(OnParticipantLeft));
        private readonly ManagerEvent<Action<Participant>> _onParticipantChanged = new ManagerEvent<Action<Participant>>(nameof(OnParticipantChanged));
        private readonly ManagerEvent<Action<Participant, string>> _onParticipantDataChanged = new ManagerEvent<Action<Participant, string>>(nameof(OnParticipantDataChanged));
        private readonly ManagerEvent<Action<string, string>> _onLobbyDataChanged = new ManagerEvent<Action<string, string>>(nameof(OnLobbyDataChanged));
        private readonly ManagerEvent<Action<Participant, string>> _onChatReceived = new ManagerEvent<Action<Participant, string>>(nameof(OnChatReceived));
        private readonly ManagerEvent<Action<SessionRules>> _onRulesChanged = new ManagerEvent<Action<SessionRules>>(nameof(OnRulesChanged));
        private readonly ManagerEvent<Action<ReadOnlyMemory<byte>>> _onGameStarted = new ManagerEvent<Action<ReadOnlyMemory<byte>>>(nameof(OnGameStarted));
        private readonly ManagerEvent<Action> _onReturnedToLobby = new ManagerEvent<Action>(nameof(OnReturnedToLobby));
        private readonly ManagerEvent<Action<LobbyId>> _onJoinRequested = new ManagerEvent<Action<LobbyId>>(nameof(OnJoinRequested));
        private readonly ManagerEvent<Action<Participant>> _onRateLimitExceeded = new ManagerEvent<Action<Participant>>(nameof(OnRateLimitExceeded));
        private readonly ManagerEvent<Action<Participant>> _onParticipantDisconnected = new ManagerEvent<Action<Participant>>(nameof(OnParticipantDisconnected));
        private readonly ManagerEvent<Action<Participant>> _onParticipantReconnected = new ManagerEvent<Action<Participant>>(nameof(OnParticipantReconnected));
        private readonly ManagerEvent<Action<MultiplayerReason>> _onLobbyRejoinFailed = new ManagerEvent<Action<MultiplayerReason>>(nameof(OnLobbyRejoinFailed));
        private readonly ManagerEvent<Action> _onAllPlayersLoaded = new ManagerEvent<Action>(nameof(OnAllPlayersLoaded));
        private readonly ManagerEvent<Action<IReadOnlyList<PlayerId>>> _onLoadTimedOut = new ManagerEvent<Action<IReadOnlyList<PlayerId>>>(nameof(OnLoadTimedOut));

        public event Action<SessionStateChange> OnStateChanged { add => _onStateChanged.Add(value); remove => _onStateChanged.Remove(value); }
        public event Action<Participant> OnParticipantJoined { add => _onParticipantJoined.Add(value); remove => _onParticipantJoined.Remove(value); }
        public event Action<Participant, MultiplayerReason> OnParticipantLeft { add => _onParticipantLeft.Add(value); remove => _onParticipantLeft.Remove(value); }
        public event Action<Participant> OnParticipantChanged { add => _onParticipantChanged.Add(value); remove => _onParticipantChanged.Remove(value); }
        public event Action<Participant, string> OnParticipantDataChanged { add => _onParticipantDataChanged.Add(value); remove => _onParticipantDataChanged.Remove(value); }
        public event Action<string, string> OnLobbyDataChanged { add => _onLobbyDataChanged.Add(value); remove => _onLobbyDataChanged.Remove(value); }
        public event Action<Participant, string> OnChatReceived { add => _onChatReceived.Add(value); remove => _onChatReceived.Remove(value); }
        public event Action<SessionRules> OnRulesChanged { add => _onRulesChanged.Add(value); remove => _onRulesChanged.Remove(value); }
        public event Action<ReadOnlyMemory<byte>> OnGameStarted { add => _onGameStarted.Add(value); remove => _onGameStarted.Remove(value); }
        public event Action OnReturnedToLobby { add => _onReturnedToLobby.Add(value); remove => _onReturnedToLobby.Remove(value); }
        public event Action<LobbyId> OnJoinRequested { add => _onJoinRequested.Add(value); remove => _onJoinRequested.Remove(value); }
        public event Action<Participant> OnRateLimitExceeded { add => _onRateLimitExceeded.Add(value); remove => _onRateLimitExceeded.Remove(value); }
        public event Action<Participant> OnParticipantDisconnected { add => _onParticipantDisconnected.Add(value); remove => _onParticipantDisconnected.Remove(value); }
        public event Action<Participant> OnParticipantReconnected { add => _onParticipantReconnected.Add(value); remove => _onParticipantReconnected.Remove(value); }
        public event Action<MultiplayerReason> OnLobbyRejoinFailed { add => _onLobbyRejoinFailed.Add(value); remove => _onLobbyRejoinFailed.Remove(value); }
        public event Action OnAllPlayersLoaded { add => _onAllPlayersLoaded.Add(value); remove => _onAllPlayersLoaded.Remove(value); }
        public event Action<IReadOnlyList<PlayerId>> OnLoadTimedOut { add => _onLoadTimedOut.Add(value); remove => _onLoadTimedOut.Remove(value); }

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

            if (!(config.ReconnectTimeoutSeconds >= 0f))
            {
                Debug.LogWarning("[Multiplayer] The reconnect timeout must be zero or more.");
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
            _reconnectTimeoutSeconds = config.ReconnectTimeoutSeconds;
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
            EndSessionIfBackendIsGone();
            UpdatePendingSearches();
            UpdateAvatarWaiters();
            UpdateAvatarRefreshes();

            UpdateHandshakeTimeouts();
            UpdateReservations();
            UpdateLobbyRejoins();
            UpdateDelayedRequests();
            UpdateLoadWait();
            UpdateJoinTimeout();
            UpdateHostClose();
            UpdateReconnect();
            UpdateLobbyRejoin();
            UpdatePingReports();
            ShowPresence();
        }

        /// <summary>
        /// Leaves any session without raising events, then lets go of the backend. The backend
        /// itself belongs to whoever created it. Every avatar texture the manager handed out is
        /// destroyed, the requests still waiting for one and the lobby searches still waiting are
        /// never answered, and the presence the session showed is removed.
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

            DisposeAvatars();
            _pendingSearches.Clear();
            _isDisposed = true;

            _onStateChanged.Clear();
            _onParticipantJoined.Clear();
            _onParticipantLeft.Clear();
            _onParticipantChanged.Clear();
            _onParticipantDataChanged.Clear();
            _onLobbyDataChanged.Clear();
            _onChatReceived.Clear();
            _onRulesChanged.Clear();
            _onGameStarted.Clear();
            _onReturnedToLobby.Clear();
            _onJoinRequested.Clear();
            _onRateLimitExceeded.Clear();
            _onParticipantDisconnected.Clear();
            _onParticipantReconnected.Clear();
            _onLobbyRejoinFailed.Clear();
            _onAllPlayersLoaded.Clear();
            _onLoadTimedOut.Clear();

            // Handlers often hold scene objects; a disposed manager must not keep them alive.
            _messages.Clear();
        }

        public bool TryGetParticipant(PlayerId player, out Participant participant)
        {
            int index = IndexOfParticipant(player);
            participant = index >= 0 ? _participants[index] : default;
            return index >= 0;
        }

        public bool TryGetConnectionQuality(PlayerId player, out ConnectionQuality quality)
        {
            quality = default;
            if (!_isInitialized || _isDisposed || !player.IsValid || player == LocalPlayerId)
            {
                return false;
            }

            ConnectionId connection;
            if (_isHost)
            {
                PeerConnection peer = FindWelcomedPeer(player);
                if (peer == null)
                {
                    return false;
                }

                connection = peer.Connection;
            }
            else if (_isWelcomed && player == _hostId && _hostConnection.IsValid)
            {
                connection = _hostConnection;
            }
            else
            {
                return false;
            }

            return _backend.TryGetConnectionQuality(connection, out quality);
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
        /// <summary>A search the backend has not answered yet, which the manager answers itself once the backend is gone.</summary>
        private sealed class PendingSearch
        {
            public Action<LobbySearchResult> Callback;
        }

        // The searches the backend has not answered yet. A search belongs to no session, so the
        // session's end does not answer it, and a backend that is gone may never do.
        private readonly List<PendingSearch> _pendingSearches = new List<PendingSearch>();

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

            // A backend that is gone may never answer, as one disposed before the manager, so
            // nothing may wait for it, and the searches that already do are answered first.
            if (!_backend.IsAvailable)
            {
                FailPendingSearches();
                Invoke(callback, new LobbySearchResult(GoneBackendReason, null));
                return;
            }

            LobbySearchQuery effective = query != null ? query.Clone() : new LobbySearchQuery();
            effective.MaxResults = Mathf.Clamp(effective.MaxResults, 1, LobbySearchQuery.MaxResultLimit);
            effective.AddStringFilter(LobbyDataKeys.GameId, _gameId);
            effective.AddNumericFilter(LobbyDataKeys.ProtocolVersion, _protocolVersion);
            effective.AddNumericFilter(LobbyDataKeys.WireVersion, WireProtocol.Version);
            effective.AddStringFilter(LobbyDataKeys.Closed, LobbyDataKeys.NotClosed);

            // Added before the request, so a backend that answers inside it, against its contract, finds it.
            PendingSearch search = new PendingSearch { Callback = callback };
            _pendingSearches.Add(search);
            _backend.SearchLobbies(effective, (reason, lobbies) => HandleSearchCompleted(search, reason, lobbies));
        }

        /// <summary>Answers the searches still waiting once the backend is no longer available.</summary>
        private void UpdatePendingSearches()
        {
            if (_pendingSearches.Count > 0 && !_backend.IsAvailable)
            {
                FailPendingSearches();
            }
        }

        /// <summary>
        /// Answers every search that waits for the backend, once, with the reason the backend
        /// names for being gone, or <see cref="MultiplayerReason.BackendError"/>
        /// (<see cref="GoneBackendReason"/>), as a backend that is no longer available may never
        /// answer them, so no game callback stays held until the manager is disposed. A backend
        /// that answers after all finds nothing waiting.
        /// </summary>
        private void FailPendingSearches()
        {
            if (_pendingSearches.Count == 0)
            {
                return;
            }

            // Emptied before any callback runs, so a callback that searches again is answered at once.
            PendingSearch[] failing = _pendingSearches.ToArray();
            _pendingSearches.Clear();

            LobbySearchResult result = new LobbySearchResult(GoneBackendReason, null);
            for (int i = 0; i < failing.Length && !_isDisposed; i++)
            {
                Invoke(failing[i].Callback, result);
            }
        }

        private void HandleSearchCompleted(PendingSearch search, MultiplayerReason reason, IReadOnlyList<BackendLobby> lobbies)
        {
            if (_isDisposed || !_pendingSearches.Remove(search))
            {
                return;
            }

            Action<LobbySearchResult> callback = search.Callback;
            if (reason != MultiplayerReason.None || lobbies == null)
            {
                Invoke(callback, new LobbySearchResult(reason == MultiplayerReason.None ? MultiplayerReason.BackendError : reason, null));
                return;
            }

            List<LobbyInfo> results = new List<LobbyInfo>(lobbies.Count);
            for (int i = 0; i < lobbies.Count; i++)
            {
                BackendLobby lobby = lobbies[i];
                if (lobby.Data == null)
                {
                    continue;
                }

                // Steam's search can hand a key back in another case than it was written in, as
                // "MAP" for "Map", so every key is found in any case, the library's own included.
                Dictionary<string, string> all = new Dictionary<string, string>(lobby.Data.Count, StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, string> pair in lobby.Data)
                {
                    all[pair.Key] = pair.Value;
                }

                // The backend filters already; checking again keeps a backend that cannot filter
                // from offering another game's or another version's lobbies, or a game that
                // refuses late joiners.
                if (!IsCompatibleLobby(all)
                    || !all.TryGetValue(LobbyDataKeys.Closed, out string closed)
                    || closed != LobbyDataKeys.NotClosed)
                {
                    continue;
                }

                Dictionary<string, string> data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, string> pair in all)
                {
                    if (!LobbyDataKeys.IsReserved(pair.Key))
                    {
                        data[pair.Key] = pair.Value;
                    }
                }

                all.TryGetValue(LobbyDataKeys.Name, out string name);
                all.TryGetValue(LobbyDataKeys.HostId, out string host);
                all.TryGetValue(LobbyDataKeys.State, out string state);

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
            public void OnAvatarChanged(PlayerId player) => _manager.HandleAvatarChanged(player);
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
                PacketKind kind = (PacketKind)data[0];
                if (IsFirstPacketDrop(kind, PacketDrop.Malformed))
                {
                    Debug.LogWarning($"[Multiplayer] Dropped a malformed packet from {remote}: {e.Message} Further malformed {PacketsOf(kind)} go unlogged until the session ends.");
                }
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
            if (newOwner == _hostId)
            {
                return;
            }

            if (_state == SessionState.Reconnecting)
            {
                EndReconnect(MultiplayerReason.HostLeft);
            }
            else if (_isWelcomed && _hostConnection.IsValid)
            {
                AwaitHostClose();
            }
            else
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
            Raise(_onJoinRequested, lobby);

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

        /// <summary>
        /// Ends the session once the backend is no longer available, as when the game shut Steam
        /// down or a Steam call failed in its native library: such a backend reports nothing
        /// more, not even its connections closing, so the session would stay as it is with no
        /// event. The reason is the one the backend names, or
        /// <see cref="MultiplayerReason.BackendError"/>.
        /// </summary>
        private void EndSessionIfBackendIsGone()
        {
            if (_state != SessionState.Offline && !_backend.IsAvailable)
            {
                EndSession(GoneBackendReason, raiseEvents: true);
            }
        }

        /// <summary>
        /// The reason the manager gives once the backend is no longer available, for a session it
        /// ends and a request it answers itself: the one the backend names, as the Steam backend
        /// names <see cref="MultiplayerReason.SteamNotRunning"/> once Steam cannot be used, or
        /// <see cref="MultiplayerReason.BackendError"/>, as for a backend disposed before the manager.
        /// </summary>
        private MultiplayerReason GoneBackendReason => UnavailableReason(MultiplayerReason.BackendError);

        /// <summary>
        /// The reason a join or a session ends with when a call to the backend fails on the way:
        /// a connection or a send to the host, or a join's reading of the lobby (#320, #338). It is
        /// <paramref name="otherwise"/> while the backend is available, and once it is not, as a
        /// Steam backend whose Steam call failed there, <see cref="GoneBackendReason"/>, the
        /// reason the next update would end the session with, so the game hears one reason
        /// whichever path noticed first.
        /// </summary>
        private MultiplayerReason FailureReason(MultiplayerReason otherwise)
        {
            return _backend.IsAvailable ? otherwise : GoneBackendReason;
        }

        /// <summary>
        /// The reason the backend names for not being available
        /// (<see cref="IUnavailableReasonBackend"/>), as the Steam backend names
        /// <see cref="MultiplayerReason.SteamNotRunning"/> once Steam cannot be used, or
        /// <paramref name="otherwise"/>.
        /// </summary>
        private MultiplayerReason UnavailableReason(MultiplayerReason otherwise)
        {
            MultiplayerReason named = _backend is IUnavailableReasonBackend backend ? backend.UnavailableReason : MultiplayerReason.None;
            return named != MultiplayerReason.None ? named : otherwise;
        }

        private void SetState(SessionState next, MultiplayerReason reason)
        {
            if (_state == next)
            {
                return;
            }

            SessionState previous = _state;
            _state = next;

            // A session starts here, so its event handler failures are logged afresh, while the
            // change to Offline that ends it still counts with it.
            if (previous == SessionState.Offline)
            {
                _loggedEventFailures.Clear();
            }

            Raise(_onStateChanged, new SessionStateChange(previous, next, reason));
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
            _gameNumber = 0;
            _participants.Clear();
            _pendingCallback = null;

            ResetHostState();
            ResetClientState();
            ResetLobbyRejoinState();
            ResetMessageState();
            ResetDataState();
            ResetLoadState();
            ResetPingState();
            ReleaseSessionAvatars();
            ResetPresenceState();
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
        // The UTF-8 bytes of the longest game ID and player name a packet carries: three for
        // each UTF-16 character, which also covers a surrogate pair and a lone surrogate. Each
        // packet reads a string with the limit of what it carries, so a longer one is malformed
        // before it is decoded.
        private const int MaxGameIdBytes = MultiplayerConfig.MaxGameIdLength * 3;
        private const int MaxPlayerNameBytes = MultiplayerLimits.MaxPlayerNameLength * 3;

        private static void WriteRules(PacketWriter writer, SessionRules rules)
        {
            writer.WriteInt(rules.Capacity);
            writer.WriteInt(rules.MinPlayersToStart);
            writer.WriteBool(rules.RequireReady);
            writer.WriteBool(rules.AllowLateJoin);
            writer.WriteInt(rules.MaxMessagesPerSecond);
            writer.WriteInt(rules.MaxBytesPerSecond);
            writer.WriteInt(BitConverter.SingleToInt32Bits(rules.RateLimitKickSeconds));
            writer.WriteInt(BitConverter.SingleToInt32Bits(rules.ReconnectReservationSeconds));
            writer.WriteInt(BitConverter.SingleToInt32Bits(rules.LoadTimeoutSeconds));
        }

        private static SessionRules ReadRules(ref PacketReader reader)
        {
            int capacity = reader.ReadInt();
            int minPlayersToStart = reader.ReadInt();
            bool requireReady = reader.ReadBool();
            bool allowLateJoin = reader.ReadBool();
            int maxMessagesPerSecond = reader.ReadInt();
            int maxBytesPerSecond = reader.ReadInt();
            float rateLimitKickSeconds = BitConverter.Int32BitsToSingle(reader.ReadInt());
            float reconnectReservationSeconds = BitConverter.Int32BitsToSingle(reader.ReadInt());
            float loadTimeoutSeconds = BitConverter.Int32BitsToSingle(reader.ReadInt());

            SessionRules rules = new SessionRules(capacity, minPlayersToStart, requireReady, allowLateJoin,
                maxMessagesPerSecond, maxBytesPerSecond, rateLimitKickSeconds, reconnectReservationSeconds, loadTimeoutSeconds);

            // The host checked them the same way, so only a host that skipped the check gets here.
            if (ValidateRules(rules, 1) != MultiplayerReason.None)
            {
                throw new FormatException($"Invalid rules: {rules}.");
            }

            return rules;
        }

        private static void WriteParticipant(PacketWriter writer, Participant participant)
        {
            writer.WriteULong(participant.Id.Value);
            writer.WriteString(participant.Name);
            writer.WriteBool(participant.IsReady);
            writer.WriteBool(participant.IsLateJoiner);
            writer.WriteBool(participant.IsConnected);
            writer.WriteBool(participant.IsLoaded);
            WriteData(writer, participant.Data);
        }

        private Participant ReadParticipant(ref PacketReader reader, PlayerId host)
        {
            PlayerId id = new PlayerId(reader.ReadULong());
            string name = reader.ReadString(MaxPlayerNameBytes);
            bool isReady = reader.ReadBool();
            bool isLateJoiner = reader.ReadBool();
            bool isConnected = reader.ReadBool();
            bool isLoaded = reader.ReadBool();
            Dictionary<string, string> data = ReadData(ref reader, MultiplayerLimits.MaxParticipantDataSize);

            if (!id.IsValid)
            {
                throw new FormatException("A participant has no player ID.");
            }

            return new Participant(id, name, id == host, id == LocalPlayerId, isReady, isLateJoiner, isConnected, isLoaded,
                data.Count > 0 ? new ReadOnlyDictionary<string, string>(data) : null);
        }
        #endregion

        #region Events
        /// <summary>
        /// The handlers of one manager event, under the event's name, which the log of a
        /// handler's exception counts it by. Subscribing and unsubscribing build the list of
        /// handlers anew, so a raise reads it without allocating and invokes each handler on its
        /// own: one handler's exception does not keep the event from the handlers after it. A
        /// raise keeps the list it started with, so a handler that subscribes or unsubscribes
        /// during a raise changes the next one, as it would with a multicast delegate.
        /// </summary>
        private sealed class ManagerEvent<THandler> where THandler : Delegate
        {
            public readonly string Name;

            private readonly object _gate = new object();

            // Delegate.Combine and Delegate.Remove keep the order and the removal rules of a
            // field-like event; the list of handlers is built from what they leave.
            private THandler _combined;
            private THandler[] _handlers = Array.Empty<THandler>();

            public ManagerEvent(string name)
            {
                Name = name;
            }

            public THandler[] Handlers => Volatile.Read(ref _handlers);

            public void Add(THandler handler)
            {
                if (handler == null)
                {
                    return;
                }

                lock (_gate)
                {
                    _combined = (THandler)Delegate.Combine(_combined, handler);
                    Volatile.Write(ref _handlers, ToArray(_combined));
                }
            }

            public void Remove(THandler handler)
            {
                if (handler == null)
                {
                    return;
                }

                lock (_gate)
                {
                    _combined = (THandler)Delegate.Remove(_combined, handler);
                    Volatile.Write(ref _handlers, ToArray(_combined));
                }
            }

            public void Clear()
            {
                lock (_gate)
                {
                    _combined = null;
                    Volatile.Write(ref _handlers, Array.Empty<THandler>());
                }
            }

            private static THandler[] ToArray(THandler combined)
            {
                if (combined == null)
                {
                    return Array.Empty<THandler>();
                }

                Delegate[] invocations = combined.GetInvocationList();
                THandler[] handlers = new THandler[invocations.Length];
                for (int i = 0; i < invocations.Length; i++)
                {
                    handlers[i] = (THandler)invocations[i];
                }

                return handlers;
            }
        }

        // Each handler runs on its own, so a handler's exception does not keep the event from the
        // handlers after it, and the event's name counts the exception for the log.
        private void Raise(ManagerEvent<Action> raised)
        {
            Action[] handlers = raised.Handlers;
            for (int i = 0; i < handlers.Length; i++)
            {
                try
                {
                    handlers[i]();
                }
                catch (Exception e)
                {
                    LogEventFailure(raised.Name, e);
                }
            }
        }

        private void Raise<T>(ManagerEvent<Action<T>> raised, T arg)
        {
            Action<T>[] handlers = raised.Handlers;
            for (int i = 0; i < handlers.Length; i++)
            {
                try
                {
                    handlers[i](arg);
                }
                catch (Exception e)
                {
                    LogEventFailure(raised.Name, e);
                }
            }
        }

        private void Raise<T1, T2>(ManagerEvent<Action<T1, T2>> raised, T1 arg1, T2 arg2)
        {
            Action<T1, T2>[] handlers = raised.Handlers;
            for (int i = 0; i < handlers.Length; i++)
            {
                try
                {
                    handlers[i](arg1, arg2);
                }
                catch (Exception e)
                {
                    LogEventFailure(raised.Name, e);
                }
            }
        }

        /// <summary>
        /// Logs an exception that a handler of a manager event threw, the first time the handlers
        /// of that event throw that exception type in a session, whether a packet from another
        /// player or a call of this player's own raised the event. Many events come from both,
        /// such as <see cref="OnStateChanged"/> or the host's own <see cref="OnLobbyDataChanged"/>,
        /// so one rule logs a handler's failure the same way wherever the event came from, and
        /// another player's chat flood cannot become a log flood. The first one keeps its stack trace.
        /// </summary>
        private void LogEventFailure(string eventName, Exception e)
        {
            Type type = e.GetType();
            if (!_loggedEventFailures.Add((eventName, type)))
            {
                return;
            }

            Debug.LogException(e);
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}",
                $"[Multiplayer] A handler of {eventName} threw {type.Name}. Further {type.Name}s from the handlers of {eventName} go unlogged until another session starts.");
        }

        /// <summary>
        /// Answers the callback of a call of this player's own. Each callback answers one call,
        /// so every exception it throws is logged.
        /// </summary>
        private static void Invoke<T>(Action<T> callback, T result)
        {
            if (callback == null)
            {
                return;
            }

            try
            {
                callback(result);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
        #endregion
    }
}
