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
    /// The assembly compiles only with Steamworks.NET 2025.164.1 or later while the build target
    /// is a standalone platform. Game code that uses this type needs the same gate: an assembly
    /// that references <c>Mu3Library.Multiplayer.Steam</c> with matching constraints, or a
    /// <c>versionDefines</c> entry for <c>com.rlabrecque.steamworks.net</c> in the game's assembly
    /// definition and <c>#if</c> on that symbol and <c>UNITY_STANDALONE</c>, as the Multiplayer
    /// sample does. Avoid <c>STEAMWORKS_NET</c>: the Steamworks.NET installer writes it into the
    /// project's scripting defines, where it stays after the package is removed. Dispose the
    /// manager before this backend, so the other players learn the reason the session ended
    /// before Steam shuts down.
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
        /// Steam cannot start, <see cref="MultiplayerReason.InvalidArgument"/> for unusable
        /// options, and <see cref="MultiplayerReason.BackendError"/> for other failures.
        /// </returns>
        public static MultiplayerResult TryCreate(SteamBackendOptions options, out SteamBackend backend)
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
                        return MultiplayerResult.Failure(SteamReasons.FromInitResult(result));
                    }

                    ownsSteam = true;
                }

                backend = new SteamBackend(options, ownsSteam);
                return MultiplayerResult.Success;
            }
            catch (Exception e) when (e is DllNotFoundException || e is InvalidOperationException)
            {
                Debug.LogWarning($"[Multiplayer] Steam could not start: {e.Message}");
                if (ownsSteam)
                {
                    SteamAPI.Shutdown();
                }

                return MultiplayerResult.Failure(MultiplayerReason.SteamNotRunning);
            }
        }

        /// <summary>
        /// Creates the Steam backend, or in the editor, when Steam cannot start and
        /// <see cref="SteamBackendOptions.EditorFallbackNetwork"/> is set, a
        /// <see cref="LocalBackend"/> with a warning. A player build never falls back, and
        /// unusable options never do.
        /// </summary>
        public static MultiplayerResult CreateOrFallback(SteamBackendOptions options, out IMultiplayerBackend backend)
        {
            MultiplayerResult result = TryCreate(options, out SteamBackend steam);
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
