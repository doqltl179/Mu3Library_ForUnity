namespace Mu3Library.SessionSave
{
    public enum SessionSaveFailureReason
    {
        None = 0,

        /// <summary>The save system refused or failed; <see cref="SessionSaveResult.Save"/> says why.</summary>
        SaveFailed,
        /// <summary>Only the host of a session may do this.</summary>
        NotHost,
        /// <summary>The player is not in the session, or is this player where another one is needed.</summary>
        PlayerNotInSession,
        /// <summary>No share with this ID is in a state the call can act on.</summary>
        ShareNotFound,
        /// <summary>A share to this player is already running.</summary>
        ShareInProgress,
        /// <summary>A message could not leave; <see cref="SessionSaveResult.Multiplayer"/> says why.</summary>
        SendFailed,
    }
}
