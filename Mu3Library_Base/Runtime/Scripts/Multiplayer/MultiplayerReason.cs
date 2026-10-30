namespace Mu3Library.Multiplayer
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

        /// <summary>The manager has not been initialized with a backend.</summary>
        NotInitialized = 1,
        /// <summary>The Steam client is not running or Steam could not be initialized.</summary>
        SteamNotRunning = 2,
        /// <summary>The Steam overlay is turned off or unavailable.</summary>
        OverlayUnavailable = 3,
        /// <summary>The backend refused or failed the request for a reason it does not name.</summary>
        BackendError = 4,

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

        /// <summary>The lobby does not exist or no longer runs a session.</summary>
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
        /// <summary>The connection to the host could not be made.</summary>
        ConnectionFailed = 26,
        /// <summary>The request did not finish in time.</summary>
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
