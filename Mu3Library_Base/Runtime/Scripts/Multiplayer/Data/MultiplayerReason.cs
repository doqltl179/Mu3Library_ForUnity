namespace Mu3Library.Multiplayer.Data
{
    /// <summary>
    /// Why a request failed or a session ended. The library never builds player-facing text;
    /// a game maps these codes to its own localized messages.
    /// </summary>
    /// <remarks>
    /// The values travel between players when a connection closes, so a value is never renumbered
    /// or reused.
    /// </remarks>
    public enum MultiplayerReason
    {
        None = 0,

        /// <summary>
        /// The manager has not been initialized with a backend, or has been disposed. The Steam
        /// backend's invite requests (<c>SteamBackend.OpenInviteDialog</c> and <c>InviteFriend</c>)
        /// answer it once that backend is disposed; while Steam cannot be used they answer
        /// <see cref="SteamNotRunning"/>, as the lobby requests do.
        /// </summary>
        NotInitialized = 1,
        /// <summary>
        /// Steam cannot be used. <c>SteamBackend.TryCreate</c> answers it when the Steam client is
        /// missing, Steam could not start, or Steam's native library could not be loaded (FR-1,
        /// FR-39). A Steam backend answers it for a lobby creation, entry, or search, an avatar,
        /// and an invite request once the game shut Steam down or a Steam call failed in its
        /// native library (FR-1), the requests that Steam had not answered yet included, and for a
        /// lobby creation that failed because Steam had no connection. Once its Steam backend
        /// can no longer use Steam, the manager answers it at once to <c>CreateLobby</c>, which
        /// cannot start taking connections then, <c>SearchLobbies</c>, and <c>LoadAvatar</c>, and
        /// ends a session with it on its next <c>Update</c>, or at once for a client whose
        /// connection to the host or reliable send to it fails there. A join ends with it too when
        /// reading the lobby's data or owner, or trying the host past a lobby that refused it,
        /// fails there.
        /// </summary>
        SteamNotRunning = 2,
        /// <summary>The Steam overlay is turned off or unavailable.</summary>
        OverlayUnavailable = 3,
        /// <summary>
        /// The backend refused or failed the request for a reason it does not name. The manager
        /// also ends a session with it, and answers it to a lobby search and an avatar request,
        /// once its backend stops being available for a reason the backend does not name, as one
        /// disposed before the manager; there it stands for <see cref="SteamNotRunning"/> wherever
        /// a Steam backend answers that, a failed join step included.
        /// </summary>
        BackendError = 4,
        /// <summary>The backend does not offer the feature, as the local backend has no avatars.</summary>
        NotSupported = 5,

        /// <summary>The request does not fit the current session state.</summary>
        InvalidState = 10,
        /// <summary>A request argument is missing or out of range.</summary>
        InvalidArgument = 11,
        /// <summary>The capacity is outside the supported range or below the current player count.</summary>
        InvalidCapacity = 12,
        /// <summary>Only the host may make this request.</summary>
        NotHost = 13,
        /// <summary>The minimum player count or the ready check is not met.</summary>
        StartConditionNotMet = 14,

        /// <summary>
        /// The lobby does not exist or no longer runs a session: a lobby without host data or
        /// whose owner is not its host, a connection that a host running no session closed, and
        /// a Steam lobby that refused the entry (Steam's <c>NotAllowed</c>) while its lobby data
        /// does not show a game running, such as an invite-only lobby entered without an invite.
        /// A join whose backend stops being available as it reads the lobby ends with the reason
        /// the session ends with instead (<see cref="SteamNotRunning"/> or <see cref="BackendError"/>),
        /// as it does for <see cref="GameInProgress"/> and <see cref="VersionMismatch"/>.
        /// </summary>
        LobbyNotFound = 20,
        /// <summary>The session is full.</summary>
        SessionFull = 21,
        /// <summary>The game is running and the session does not take late joiners.</summary>
        GameInProgress = 22,
        /// <summary>The game identifier or protocol version differs from the host's.</summary>
        VersionMismatch = 23,
        /// <summary>The same account is already in the session.</summary>
        DuplicateAccount = 24,
        /// <summary>The host kicked this account from this session.</summary>
        Banned = 25,
        /// <summary>
        /// A connection could not be made: the client could not connect to the host, could not
        /// send its hello, or its connection closed during the join without a reason from the
        /// library; and the host could not start listening for connections when it created a
        /// lobby (FR-29). On a Steam backend that can no longer use Steam the lobby creation
        /// answers <see cref="SteamNotRunning"/> instead, and so does a join whose connection or
        /// hello fails there; on another backend that is no longer available such a join ends
        /// with <see cref="BackendError"/>, as the session's end then does.
        /// </summary>
        ConnectionFailed = 26,
        /// <summary>
        /// The request did not finish in time: the join (<see cref="MultiplayerConfig.JoinTimeoutSeconds"/>),
        /// the host's handshake (<see cref="MultiplayerConfig.HandshakeTimeoutSeconds"/>), a Steam
        /// lobby search (25 seconds) or avatar (10 seconds), and a lobby rejoin that got no answer
        /// (<see cref="IMultiplayerManagerEventBus.OnLobbyRejoinFailed"/>). The host also closes
        /// with it a connection of an account that already has
        /// <see cref="MultiplayerLimits.MaxPendingConnectionsPerAccount"/> connections it has not
        /// admitted. That is no timeout, but a reconnecting client tries again on it, as on a
        /// handshake that ran out, since an earlier attempt of its own may still wait at the host.
        /// </summary>
        Timeout = 27,

        /// <summary>The player left on their own.</summary>
        LeftNormally = 30,
        /// <summary>The host kicked the player.</summary>
        Kicked = 31,
        /// <summary>The host left, lost its connection, or stopped.</summary>
        HostLeft = 32,
        /// <summary>The connection dropped and did not come back.</summary>
        ConnectionLost = 33,

        /// <summary>A message exceeds the size limit.</summary>
        MessageTooLarge = 40,
        /// <summary>A sender exceeded its send rate limit.</summary>
        RateLimited = 41,
    }
}
