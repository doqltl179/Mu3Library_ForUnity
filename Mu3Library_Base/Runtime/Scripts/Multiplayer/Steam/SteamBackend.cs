using System;
using System.Collections.Generic;
using System.Globalization;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Local;
using Steamworks;
using UnityEngine;

namespace Mu3Library.Multiplayer.Steam
{
    /// <summary>
    /// A backend on Steam: Steam lobbies to find and enter sessions, and Steam networking sockets
    /// for the connections, which by default go only through Valve's relays.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Steam reports through callbacks whenever <c>SteamAPI.RunCallbacks</c> runs, which may be
    /// the game's own call. The backend queues those reports and hands them to its listener only
    /// from <see cref="Update"/>, as the backend contract asks.
    /// </para>
    /// <para>
    /// The assembly compiles only with Steamworks.NET 2025.164.1 or later, installed through the
    /// Package Manager, and only in the editor and in 64-bit standalone players (Windows x64,
    /// macOS, Linux x64): its <c>includePlatforms</c> leave out 32-bit Windows players. Game code
    /// that uses this type needs the same gate, preferably an assembly definition of its own that
    /// references <c>Mu3Library.Multiplayer.Steam</c> with the same <c>includePlatforms</c>,
    /// <c>defineConstraints</c>, and <c>versionDefines</c> entry, since a <c>versionDefines</c>
    /// symbol is defined only in the assembly that declares it. <c>#if</c> on such a symbol and
    /// <c>UNITY_STANDALONE</c> also holds in a 32-bit Windows player, where this assembly is
    /// missing, and Unity 6.6 deprecates <c>UNITY_64</c>, so that gate suits only a game that
    /// builds no 32-bit Windows player, as the Multiplayer sample does. The Optional Packages
    /// section of the repository README describes the gate. Dispose the manager before this
    /// backend, so the other players learn the reason the session ended before Steam shuts down.
    /// </para>
    /// <para>
    /// Steam publishes lobby data a moment after it is set, so a new lobby can take a few seconds
    /// to show up in search.
    /// </para>
    /// <para>
    /// A Steam native library can lack a function that only a later call needs, as an outdated
    /// <c>steam_api64</c> that still starts Steam may. Every Steam call the backend makes once it
    /// exists, Steam's callbacks that it runs and handles included, catches what
    /// <see cref="TryCreate"/> turns into a reason, such as the
    /// <see cref="EntryPointNotFoundException"/> of a missing function, so no such exception
    /// reaches the game. The backend logs the first one as a warning and is no longer available
    /// (<see cref="IsAvailable"/>): from then on it answers as when the game shut Steam down under
    /// it. A lobby creation, entry, or search answers <see cref="MultiplayerReason.SteamNotRunning"/>,
    /// the ones Steam had not answered yet included, and so does an invite request; an avatar
    /// answers <see cref="MultiplayerReason.BackendError"/>, as <see cref="LoadAvatar"/> describes;
    /// the calls that answer at once return false, <see cref="ConnectionId.None"/>,
    /// <see cref="PlayerId.None"/>, or null; and an open connection carries nothing more. The
    /// reason is <see cref="MultiplayerReason.SteamNotRunning"/> for the reason
    /// <see cref="TryCreate"/> gives it: for the game, Steam no longer works in this process.
    /// </para>
    /// <para>
    /// A <see cref="MultiplayerManager"/> on this backend answers
    /// <see cref="MultiplayerReason.SteamNotRunning"/> at once to
    /// <see cref="MultiplayerManager.CreateLobby"/> once the backend is not available because
    /// Steam cannot be used, after a failure as once the game shut Steam down.
    /// </para>
    /// <para>
    /// The backend catches the exception at its calls rather than checking at creation that every
    /// function it calls exists: such a check would need a hand-kept list of every native function
    /// the backend calls, reached through Steamworks.NET's internal <c>NativeMethods</c> by
    /// reflection, which a later change to the backend or to Steamworks.NET can leave behind
    /// without a compile error, while the catch covers every call the backend makes.
    /// </para>
    /// </remarks>
    public sealed partial class SteamBackend : IMultiplayerBackend, IUnavailableReasonBackend
    {
        private enum EventKind
        {
            Callback,
            Connected,
            Disconnected,
            OwnerChanged,
            JoinRequested,
            AvatarChanged,
        }

        private struct PendingEvent
        {
            public EventKind Kind;
            public uint Connection;
            public PlayerId Player;
            public BackendDisconnectKind DisconnectKind;
            public int ReasonCode;
            public LobbyId Lobby;
            public Action Callback;
        }

        // A launch from an invite names its lobby once per process; a backend created again later,
        // for example after returning to the title, must not join that lobby a second time.
        private static bool s_launchLobbyTaken = false;

        private readonly bool _ownsSteam;
        private readonly bool _runCallbacks;
        private readonly Queue<PendingEvent> _events = new Queue<PendingEvent>();
        private IMultiplayerBackendListener _listener;
        private bool _isDisposed = false;

        // True once a Steam call threw what IsSteamUnavailable tells; see Break.
        private bool _isBroken = false;

        // True for a backend the tests made without Steam (CreateWithoutSteam), which takes Steam for running.
        private readonly bool _assumesSteamRuns;

        // A join request that arrived while nothing listened, as the lobby a launch from an
        // invite names does; it goes to the next listener.
        private LobbyId _heldJoinRequest = LobbyId.None;

        private Callback<GameLobbyJoinRequested_t> _lobbyJoinRequested;

        public string Name => "Steam";

        /// <summary>
        /// False once disposed, once the game shut Steam down under this backend, or once a Steam
        /// call failed in Steam's native library, as the remarks on the class describe.
        /// </summary>
        public bool IsAvailable => !_isDisposed && !_isBroken && SteamRuns;

        /// <summary>
        /// <see cref="MultiplayerReason.SteamNotRunning"/> while Steam cannot be used, the reason
        /// the lobby requests answer then, and nothing once the backend is disposed, which leaves
        /// the manager its own reason.
        /// </summary>
        MultiplayerReason IUnavailableReasonBackend.UnavailableReason => _isDisposed ? MultiplayerReason.None : MultiplayerReason.SteamNotRunning;

        public PlayerId LocalPlayerId { get; }
        public int MaxPacketSize => Constants.k_cbMaxSteamNetworkingSocketsMessageSizeSend;

        /// <summary>True when this backend started Steam, and so shuts it down when disposed.</summary>
        public bool OwnsSteam => _ownsSteam;

        // Whether Steamworks.NET runs, which a backend made without Steam takes for granted.
        private bool SteamRuns => _assumesSteamRuns || CallbackDispatcher.IsInitialized;



        private SteamBackend(SteamBackendOptions options, bool ownsSteam, bool withoutSteam = false)
        {
            _ownsSteam = ownsSteam;
            _runCallbacks = options.RunSteamCallbacks;
            _assumesSteamRuns = withoutSteam;

            // Every native call comes before the first callback registration and the pinned send
            // buffer, so a native library that throws here, which TryCreate turns into a reason,
            // leaves nothing behind: no callback is registered and no buffer pinned yet, and
            // TryCreate shuts down a Steam it started. Keep new native calls in this block.
            if (!withoutSteam)
            {
                LocalPlayerId = new PlayerId(SteamUser.GetSteamID().m_SteamID);
                SteamNetworkingUtils.InitRelayNetworkAccess();
                _pollGroup = SteamNetworkingSockets.CreatePollGroup();
            }

            InitializeConnections(options);
            InitializeLobbies();
            InitializePresence(options);
            InitializeAvatars();
            _lobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(HandleLobbyJoinRequested);

            if (!s_launchLobbyTaken && SteamLaunchArguments.TryGetConnectLobby(Environment.GetCommandLineArgs(), out LobbyId lobby))
            {
                s_launchLobbyTaken = true;
                _events.Enqueue(new PendingEvent { Kind = EventKind.JoinRequested, Lobby = lobby });
            }
        }

        /// <summary>
        /// Creates the Steam backend. When Steamworks.NET has not been started yet, the backend
        /// starts it with <see cref="SteamBackendOptions.AppId"/> and shuts it down when disposed;
        /// otherwise it uses the running Steam and leaves it running.
        /// </summary>
        /// <returns>
        /// <see cref="MultiplayerReason.SteamNotRunning"/> when the Steam client is not running or
        /// Steam cannot start, a Steam native library that cannot be loaded or lacks a function
        /// included, <see cref="MultiplayerReason.InvalidArgument"/> for unusable options, and
        /// <see cref="MultiplayerReason.BackendError"/> for other failures that Steam reports.
        /// </returns>
        /// <remarks>
        /// <para>
        /// When the runtime cannot load the native library or find a function in it, it throws:
        /// <see cref="DllNotFoundException"/>, <see cref="EntryPointNotFoundException"/>, or
        /// <see cref="BadImageFormatException"/>, also wrapped in the
        /// <see cref="TypeInitializationException"/> of a type whose initializer met one. Which of
        /// them a missing, outdated, or wrong-architecture <c>steam_api64</c> causes depends on the
        /// runtime; Unity's Mono and IL2CPP report most libraries that fail to load as
        /// <see cref="DllNotFoundException"/>. The backend logs the exception's type and message as
        /// a warning and answers <see cref="MultiplayerReason.SteamNotRunning"/> for any of them,
        /// so no such exception reaches the game and <see cref="CreateOrFallback"/> falls back on
        /// it. It is <see cref="MultiplayerReason.SteamNotRunning"/> rather than
        /// <see cref="MultiplayerReason.BackendError"/> because, for the game, nothing of Steam
        /// works in this process, as when the library is missing, which answered it already. Any
        /// other exception is a mistake in the code and reaches the caller.
        /// </para>
        /// <para>
        /// An outdated library that loads, but whose <c>SteamAPI.InitEx</c> answers a version
        /// mismatch, throws nothing and answers <see cref="MultiplayerReason.BackendError"/>, the
        /// reason for a failure that Steam reports; the editor falls back on that reason too.
        /// </para>
        /// </remarks>
        public static MultiplayerResult TryCreate(SteamBackendOptions options, out SteamBackend backend)
        {
            return TryCreateWith(options, StartSteam, out backend);
        }

        /// <summary>
        /// The native part of <see cref="TryCreate"/>: starts
        /// Steam when it does not run yet, setting <paramref name="ownsSteam"/> once it did, and
        /// makes the backend. The tests hand in one that throws what a broken native library throws.
        /// </summary>
        internal delegate MultiplayerReason SteamStarter(SteamBackendOptions options, ref bool ownsSteam, out SteamBackend backend);

        internal static MultiplayerResult TryCreateWith(SteamBackendOptions options, SteamStarter start, out SteamBackend backend)
        {
            backend = null;
            options ??= new SteamBackendOptions();
            if (!(options.ConnectionTimeoutSeconds > 0f) || !IsValidDisplayToken(options.PresenceDisplayToken))
            {
                return MultiplayerResult.Failure(MultiplayerReason.InvalidArgument);
            }

            bool ownsSteam = false;
            try
            {
                MultiplayerReason reason = start(options, ref ownsSteam, out backend);
                return reason == MultiplayerReason.None ? MultiplayerResult.Success : MultiplayerResult.Failure(reason);
            }
            catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
            {
                backend = null;
                Debug.LogWarning($"[Multiplayer] Steam could not start ({cause.GetType().Name}): {cause.Message}");
                if (ownsSteam)
                {
                    SteamAPI.Shutdown();
                }

                return MultiplayerResult.Failure(MultiplayerReason.SteamNotRunning);
            }
        }

        private static MultiplayerReason StartSteam(SteamBackendOptions options, ref bool ownsSteam, out SteamBackend backend)
        {
            backend = null;

            // The dispatcher, not the native client, tells whether Steamworks.NET runs: after a
            // domain reload the native Steam stays up while the dispatcher starts over, and
            // without it no callback would ever arrive. Steam counts its own initializations.
            if (!CallbackDispatcher.IsInitialized)
            {
                if (options.AppId != 0)
                {
                    // Steam reads these before steam_appid.txt; a launch through Steam sets them itself.
                    string appId = options.AppId.ToString(CultureInfo.InvariantCulture);
                    Environment.SetEnvironmentVariable("SteamAppId", appId);
                    Environment.SetEnvironmentVariable("SteamGameId", appId);
                }

                ESteamAPIInitResult result = SteamAPI.InitEx(out string message);
                if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                {
                    Debug.LogWarning($"[Multiplayer] Steam could not start ({result}): {message}");
                    return SteamReasons.FromInitResult(result);
                }

                ownsSteam = true;
            }

            backend = new SteamBackend(options, ownsSteam);
            return MultiplayerReason.None;
        }

        /// <summary>
        /// Test hook: a backend that reaches no Steam while it is made and takes Steam for running
        /// afterwards, so every request goes on to Steamworks.NET. With Steam not started in this
        /// process, Steamworks.NET throws <see cref="InvalidOperationException"/> from the calls
        /// where an outdated native library throws <see cref="EntryPointNotFoundException"/>, and
        /// the backend takes both alike (<see cref="IsSteamUnavailable"/>).
        /// </summary>
        internal static SteamBackend CreateWithoutSteam(SteamBackendOptions options)
        {
            return new SteamBackend(options ?? new SteamBackendOptions(), ownsSteam: false, withoutSteam: true);
        }

        /// <summary>
        /// True for an exception that tells Steam cannot run in this process, with the exception
        /// that caused it: Steamworks.NET's own checks (<see cref="InvalidOperationException"/>),
        /// and a native library that is missing, lacks a function, or is built for another
        /// architecture, also inside a <see cref="TypeInitializationException"/>. A backend that
        /// exists catches the same at every Steam call, and then calls <see cref="Break"/>.
        /// </summary>
        internal static bool IsSteamUnavailable(Exception exception, out Exception cause)
        {
            cause = exception;
            while (cause is TypeInitializationException && cause.InnerException != null)
            {
                cause = cause.InnerException;
            }

            return cause is DllNotFoundException
                || cause is EntryPointNotFoundException
                || cause is BadImageFormatException
                || cause is InvalidOperationException;
        }

        /// <summary>
        /// Makes the backend unavailable after a Steam call threw what
        /// <see cref="IsSteamUnavailable"/> tells, as the remarks on the class describe, and logs
        /// the first such call. The requests waiting on Steam answer on the next
        /// <see cref="Update"/>.
        /// </summary>
        internal void Break(Exception cause)
        {
            if (_isBroken)
            {
                return;
            }

            _isBroken = true;
            Debug.LogWarning($"[Multiplayer] The Steam backend is no longer available: a Steam call failed ({cause.GetType().Name}): {cause.Message}");
        }

        /// <summary>
        /// Creates the Steam backend, or in the editor, when Steam cannot start and
        /// <see cref="SteamBackendOptions.EditorFallbackNetwork"/> is set, a
        /// <see cref="LocalBackend"/> with a warning. A broken Steam native library counts as
        /// Steam that cannot start, as <see cref="TryCreate"/>
        /// describes. A player build never falls back, and unusable options never do.
        /// </summary>
        public static MultiplayerResult CreateOrFallback(SteamBackendOptions options, out IMultiplayerBackend backend)
        {
            return CreateOrFallbackWith(options, StartSteam, out backend);
        }

        internal static MultiplayerResult CreateOrFallbackWith(SteamBackendOptions options, SteamStarter start, out IMultiplayerBackend backend)
        {
            MultiplayerResult result = TryCreateWith(options, start, out SteamBackend steam);
            if (result.IsSuccess)
            {
                backend = steam;
                return result;
            }

            backend = ChooseFallback(result.Reason, Application.isEditor, options);
            return backend != null ? MultiplayerResult.Success : result;
        }

        internal static IMultiplayerBackend ChooseFallback(MultiplayerReason reason, bool isEditor, SteamBackendOptions options)
        {
            bool steamUnavailable = reason == MultiplayerReason.SteamNotRunning || reason == MultiplayerReason.BackendError;
            if (!steamUnavailable || !isEditor || options?.EditorFallbackNetwork == null)
            {
                return null;
            }

            Debug.LogWarning($"[Multiplayer] Steam is unavailable ({reason}); the editor uses the local backend instead.");
            return new LocalBackend(options.EditorFallbackNetwork, options.EditorFallbackPlayerName);
        }

        public string GetPlayerName(PlayerId player)
        {
            if (!IsAvailable)
            {
                return player.ToString();
            }

            try
            {
                return player == LocalPlayerId
                    ? SteamFriends.GetPersonaName()
                    : SteamFriends.GetFriendPersonaName(new CSteamID(player.Value));
            }
            catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
            {
                Break(cause);
                return player.ToString();
            }
        }

        public void SetListener(IMultiplayerBackendListener listener)
        {
            _listener = listener;

            if (listener != null && _heldJoinRequest.IsValid && !_isDisposed)
            {
                _events.Enqueue(new PendingEvent { Kind = EventKind.JoinRequested, Lobby = _heldJoinRequest });
                _heldJoinRequest = LobbyId.None;
            }
        }

        public void Update()
        {
            if (_isDisposed)
            {
                return;
            }

            // With Steam shut down under the backend, or a native library that failed a call,
            // nothing native runs, but every request still answers: the queued answers go out
            // and the requests waiting on Steam fail.
            if (IsAvailable && _runCallbacks)
            {
                try
                {
                    SteamAPI.RunCallbacks();
                }
                catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
                {
                    Break(cause);
                }
            }

            DisposeFinishedCalls();
            CheckSearchTimeout();
            CheckRefusalTimeouts();
            if (IsAvailable)
            {
                UpdateAvatars();
            }

            // Checked again, since a call above may have failed.
            if (!IsAvailable)
            {
                // An avatar answers the reason the manager gives then; see LoadAvatar.
                FailSearches(MultiplayerReason.SteamNotRunning);
                FailLobbyCalls(MultiplayerReason.SteamNotRunning);
                FailAvatars(MultiplayerReason.BackendError);
            }

            // What a callback queues while being handed on waits for the next update.
            int count = _events.Count;
            for (int i = 0; i < count && !_isDisposed; i++)
            {
                Dispatch(_events.Dequeue());
            }

            if (IsAvailable)
            {
                ReceiveMessages();
            }
        }

        #region Invites
        /// <summary>Opens the Steam overlay's dialog for inviting friends to the lobby.</summary>
        /// <returns>
        /// <see cref="MultiplayerReason.OverlayUnavailable"/> when the overlay is turned off, and
        /// what <see cref="InviteFriend"/> answers for an unavailable backend or an invalid lobby.
        /// </returns>
        public MultiplayerResult OpenInviteDialog(LobbyId lobby)
        {
            if (!IsAvailable)
            {
                return MultiplayerResult.Failure(UnavailableReason());
            }

            if (!lobby.IsValid)
            {
                return MultiplayerResult.Failure(MultiplayerReason.InvalidArgument);
            }

            try
            {
                if (!SteamUtils.IsOverlayEnabled())
                {
                    return MultiplayerResult.Failure(MultiplayerReason.OverlayUnavailable);
                }

                SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(lobby.Value));
                return MultiplayerResult.Success;
            }
            catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
            {
                Break(cause);
                return MultiplayerResult.Failure(MultiplayerReason.SteamNotRunning);
            }
        }

        /// <summary>Sends a friend an invite to the lobby through Steam.</summary>
        /// <returns>
        /// <see cref="MultiplayerReason.NotInitialized"/> once the backend is disposed, as a
        /// disposed manager answers; <see cref="MultiplayerReason.SteamNotRunning"/> while Steam
        /// cannot be used, because the game shut it down or a Steam call failed in its native
        /// library, as the lobby requests answer then; <see cref="MultiplayerReason.InvalidArgument"/>
        /// for an invalid lobby or friend; and <see cref="MultiplayerReason.BackendError"/> when
        /// Steam refuses the invite.
        /// </returns>
        public MultiplayerResult InviteFriend(LobbyId lobby, PlayerId friend)
        {
            if (!IsAvailable)
            {
                return MultiplayerResult.Failure(UnavailableReason());
            }

            if (!lobby.IsValid || !friend.IsValid)
            {
                return MultiplayerResult.Failure(MultiplayerReason.InvalidArgument);
            }

            try
            {
                return SteamMatchmaking.InviteUserToLobby(new CSteamID(lobby.Value), new CSteamID(friend.Value))
                    ? MultiplayerResult.Success
                    : MultiplayerResult.Failure(MultiplayerReason.BackendError);
            }
            catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
            {
                Break(cause);
                return MultiplayerResult.Failure(MultiplayerReason.SteamNotRunning);
            }
        }

        /// <summary>
        /// Why an invite fails while the backend is not available: the backend itself is gone once
        /// it is disposed, and otherwise Steam is.
        /// </summary>
        private MultiplayerReason UnavailableReason()
        {
            return _isDisposed ? MultiplayerReason.NotInitialized : MultiplayerReason.SteamNotRunning;
        }
        #endregion

        #region Events
        private void HandleLobbyJoinRequested(GameLobbyJoinRequested_t request)
        {
            if (_isDisposed)
            {
                return;
            }

            _events.Enqueue(new PendingEvent { Kind = EventKind.JoinRequested, Lobby = new LobbyId(request.m_steamIDLobby.m_SteamID) });
        }

        private void EnqueueCallback(Action callback)
        {
            if (!_isDisposed)
            {
                _events.Enqueue(new PendingEvent { Kind = EventKind.Callback, Callback = callback });
            }
        }

        private void Dispatch(PendingEvent pending)
        {
            switch (pending.Kind)
            {
                case EventKind.Callback:
                    pending.Callback();
                    break;

                case EventKind.Connected:
                    ReportConnected(pending.Connection);
                    break;

                case EventKind.Disconnected:
                    ReportDisconnected(pending.Connection, pending.DisconnectKind, pending.ReasonCode);
                    break;

                case EventKind.OwnerChanged:
                    _listener?.OnLobbyOwnerChanged(pending.Lobby, pending.Player);
                    break;

                case EventKind.JoinRequested:
                    if (_listener != null)
                    {
                        _listener.OnLobbyJoinRequested(pending.Lobby);
                    }
                    else
                    {
                        _heldJoinRequest = pending.Lobby;
                    }
                    break;

                case EventKind.AvatarChanged:
                    _listener?.OnAvatarChanged(pending.Player);
                    break;
            }
        }
        #endregion

        /// <summary>
        /// Closes every connection, leaves every lobby, and lets go of Steam's callbacks. Steam
        /// itself shuts down only if this backend started it. When the game shut Steam down first,
        /// only the backend's own resources are released. A backend that a failed Steam call made
        /// unavailable still tries each of these, and releases its own resources either way.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            bool steamRuns = SteamRuns;
            _isDisposed = true;
            _listener = null;
            _events.Clear();
            ShutdownAvatars();

            _lobbyJoinRequested?.Dispose();
            _lobbyJoinRequested = null;

            ShutdownConnections(steamRuns);
            ShutdownLobbies(steamRuns);
            ShutdownPresence(steamRuns);

            if (_ownsSteam && steamRuns)
            {
                try
                {
                    SteamAPI.Shutdown();
                }
                catch (Exception e) when (IsSteamUnavailable(e, out Exception cause))
                {
                    Break(cause);
                }
            }
        }
    }
}
