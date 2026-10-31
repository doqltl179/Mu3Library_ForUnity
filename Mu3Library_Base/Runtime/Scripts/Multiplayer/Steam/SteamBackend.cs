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
    /// Package Manager, in the editor and in 64-bit standalone players: its <c>includePlatforms</c>
    /// leave out 32-bit Windows. Game code that uses this type needs the same gate in an assembly
    /// definition of its own, since code without one (<c>Assembly-CSharp</c>) cannot read
    /// <c>versionDefines</c>: an assembly that references <c>Mu3Library.Multiplayer.Steam</c> with
    /// the same <c>includePlatforms</c> and <c>defineConstraints</c>, or a <c>versionDefines</c>
    /// entry for <c>com.rlabrecque.steamworks.net</c> at <c>2025.164.1</c> or later and <c>#if</c>
    /// on that symbol and <c>UNITY_STANDALONE</c>, which is also true in a 32-bit Windows player,
    /// where this assembly is missing, so it suits only a game that builds no 32-bit Windows
    /// player, as the Multiplayer sample does. <c>versionDefines</c> sees
    /// only a package that the Package Manager installed, not a <c>.unitypackage</c> import. Avoid
    /// <c>STEAMWORKS_NET</c>: the Steamworks.NET installer writes it into the project's scripting
    /// defines, where it stays after the package is removed. Dispose the manager before this
    /// backend, so the other players learn the reason the session ended before Steam shuts down.
    /// </para>
    /// <para>
    /// Steam publishes lobby data a moment after it is set, so a new lobby can take a few seconds
    /// to show up in search.
    /// </para>
    /// </remarks>
    public sealed partial class SteamBackend : IMultiplayerBackend
    {
        private enum EventKind
        {
            Callback,
            Connected,
            Disconnected,
            OwnerChanged,
            JoinRequested,
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

        // A join request that arrived while nothing listened, as the lobby a launch from an
        // invite names does; it goes to the next listener.
        private LobbyId _heldJoinRequest = LobbyId.None;

        private Callback<GameLobbyJoinRequested_t> _lobbyJoinRequested;

        public string Name => "Steam";

        /// <summary>False once disposed, or once the game shut Steam down under this backend.</summary>
        public bool IsAvailable => !_isDisposed && CallbackDispatcher.IsInitialized;

        public PlayerId LocalPlayerId { get; }
        public int MaxPacketSize => Constants.k_cbMaxSteamNetworkingSocketsMessageSizeSend;

        /// <summary>True when this backend started Steam, and so shuts it down when disposed.</summary>
        public bool OwnsSteam => _ownsSteam;



        private SteamBackend(SteamBackendOptions options, bool ownsSteam)
        {
            _ownsSteam = ownsSteam;
            _runCallbacks = options.RunSteamCallbacks;
            LocalPlayerId = new PlayerId(SteamUser.GetSteamID().m_SteamID);

            InitializeConnections(options);
            InitializeLobbies();
            InitializePresence(options);
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
        /// Steam cannot start, a Steam native library that is missing, older than this
        /// Steamworks.NET, or built for another architecture included,
        /// <see cref="MultiplayerReason.InvalidArgument"/> for unusable options, and
        /// <see cref="MultiplayerReason.BackendError"/> for other failures that Steam reports.
        /// </returns>
        /// <remarks>
        /// Steamworks.NET throws when it cannot load the native library or find a function in it:
        /// <see cref="DllNotFoundException"/>, <see cref="EntryPointNotFoundException"/> for an
        /// outdated <c>steam_api64</c>, and <see cref="BadImageFormatException"/> for one of
        /// another architecture, also wrapped in the <see cref="TypeInitializationException"/> of
        /// a type whose initializer met one. The backend logs the exception's type and message as
        /// a warning and answers <see cref="MultiplayerReason.SteamNotRunning"/> instead, so no
        /// such exception reaches the game and <see cref="CreateOrFallback"/> falls back on it.
        /// It is <see cref="MultiplayerReason.SteamNotRunning"/> rather than
        /// <see cref="MultiplayerReason.BackendError"/> because, for the game, nothing of Steam
        /// works in this process, as when the library is missing, which answered it already. Any
        /// other exception is a mistake in the code and reaches the caller.
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
        /// True for an exception that tells Steam cannot run in this process, with the exception
        /// that caused it: Steamworks.NET's own checks (<see cref="InvalidOperationException"/>),
        /// and a native library that is missing, lacks a function, or is built for another
        /// architecture, also inside a <see cref="TypeInitializationException"/>.
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

            return player == LocalPlayerId
                ? SteamFriends.GetPersonaName()
                : SteamFriends.GetFriendPersonaName(new CSteamID(player.Value));
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

            // With Steam shut down under the backend, nothing native runs, but every request
            // still answers: the queued answers go out and the waiting searches fail.
            if (IsAvailable && _runCallbacks)
            {
                SteamAPI.RunCallbacks();
            }

            DisposeFinishedCalls();
            CheckSearchTimeout();
            if (IsAvailable)
            {
                UpdateAvatars();
            }
            else
            {
                FailSearches(MultiplayerReason.SteamNotRunning);
                FailAvatars(MultiplayerReason.SteamNotRunning);
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
        /// <returns><see cref="MultiplayerReason.OverlayUnavailable"/> when the overlay is turned off.</returns>
        public MultiplayerResult OpenInviteDialog(LobbyId lobby)
        {
            if (!IsAvailable)
            {
                return MultiplayerResult.Failure(MultiplayerReason.NotInitialized);
            }

            if (!lobby.IsValid)
            {
                return MultiplayerResult.Failure(MultiplayerReason.InvalidArgument);
            }

            if (!SteamUtils.IsOverlayEnabled())
            {
                return MultiplayerResult.Failure(MultiplayerReason.OverlayUnavailable);
            }

            SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(lobby.Value));
            return MultiplayerResult.Success;
        }

        /// <summary>Sends a friend an invite to the lobby through Steam.</summary>
        public MultiplayerResult InviteFriend(LobbyId lobby, PlayerId friend)
        {
            if (!IsAvailable)
            {
                return MultiplayerResult.Failure(MultiplayerReason.NotInitialized);
            }

            if (!lobby.IsValid || !friend.IsValid)
            {
                return MultiplayerResult.Failure(MultiplayerReason.InvalidArgument);
            }

            return SteamMatchmaking.InviteUserToLobby(new CSteamID(lobby.Value), new CSteamID(friend.Value))
                ? MultiplayerResult.Success
                : MultiplayerResult.Failure(MultiplayerReason.BackendError);
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
            }
        }
        #endregion

        /// <summary>
        /// Closes every connection, leaves every lobby, and lets go of Steam's callbacks. Steam
        /// itself shuts down only if this backend started it. When the game shut Steam down first,
        /// only the backend's own resources are released.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            bool steamRuns = CallbackDispatcher.IsInitialized;
            _isDisposed = true;
            _listener = null;
            _events.Clear();
            _pendingAvatars.Clear();

            _lobbyJoinRequested?.Dispose();
            _lobbyJoinRequested = null;

            ShutdownConnections(steamRuns);
            ShutdownLobbies(steamRuns);
            ShutdownPresence(steamRuns);

            if (_ownsSteam && steamRuns)
            {
                SteamAPI.Shutdown();
            }
        }
    }
}
