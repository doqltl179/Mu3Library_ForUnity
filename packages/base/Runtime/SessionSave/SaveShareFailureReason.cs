namespace Mu3Library.SessionSave
{
    /// <summary>
    /// Why a share of a session save ended without the receiving player storing it. Both players
    /// hear the same reason when the other side could tell them.
    /// </summary>
    public enum SaveShareFailureReason
    {
        None = 0,

        /// <summary>The receiving player's game declined the offer.</summary>
        Declined = 1,
        /// <summary>The receiving player was already receiving another share.</summary>
        Busy = 2,
        /// <summary>The save is larger than the receiving player's <see cref="Mu3Library.Save.ISaveManager.MaxSlotSize"/>.</summary>
        TooLarge = 3,
        /// <summary>
        /// What arrived failed the receiving player's check: its bytes were damaged or out of order,
        /// or the slot is in a format newer than that player's build reads.
        /// </summary>
        Corrupted = 4,
        /// <summary>
        /// One of the players cancelled the share, or disposed its manager; on the receiving
        /// player, also a share that a new offer from the same host replaced, since the host had
        /// ended it without reaching this player.
        /// </summary>
        Cancelled = 5,
        /// <summary>The other player did not answer within <see cref="SessionSaveManager.ShareTimeoutSeconds"/>.</summary>
        Timeout = 6,
        /// <summary>The receiving player left the session, is away, or a message could not reach them.</summary>
        PlayerLeft = 7,
        /// <summary>The session ended, or another player became its host.</summary>
        SessionEnded = 8,
        /// <summary>A message of the share could not leave.</summary>
        SendFailed = 9,
        /// <summary>The receiving player's storage could not write the slot.</summary>
        StoreFailed = 10,
    }
}
