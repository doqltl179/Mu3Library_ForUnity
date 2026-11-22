using System.Collections.Generic;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Save;

namespace Mu3Library.SessionSave
{
    /// <summary>
    /// Saves a multiplayer session through an <see cref="ISaveManager"/> and shares the save with
    /// the other players. The host keeps the session's save: the sections registered with the
    /// save manager hold what the whole session shares, and each <see cref="IPlayerSaveSection"/>
    /// is saved once for every player under their <see cref="PlayerId"/>, so a player's data
    /// stays theirs whoever hosts. Only the host saves and loads; the host can also send a saved
    /// slot to another player, who stores it as a slot of their own and can host the session from
    /// it later.
    /// </summary>
    /// <remarks>
    /// Calls run on the main thread, and the share's messages arrive from inside the multiplayer
    /// manager's <c>Update</c>; call this manager's <c>Update</c> every frame as well, which paces
    /// the share and ends one whose other player stopped answering. A failure comes back as a
    /// <see cref="SessionSaveResult"/>; only a mistake in the calling code throws, such as a null
    /// argument or a call after <c>Dispose</c>.
    /// </remarks>
    public interface ISessionSaveManager
    {
        /// <summary>
        /// The save manager the session saves through. Register the sections the whole session
        /// shares with it, and give it a storage of the session saves' own, apart from any
        /// single-player save.
        /// </summary>
        public ISaveManager Saves { get; }

        /// <summary>
        /// Largest payload of one share message, in bytes: <see cref="SessionSaveManager.DefaultShareChunkSize"/>
        /// by default, and never more than the multiplayer messenger's <c>MaxMessageSize</c> leaves.
        /// </summary>
        public int ShareChunkSize { get; set; }

        /// <summary>
        /// Most bytes a second the host sends of every share together:
        /// <see cref="SessionSaveManager.DefaultShareBytesPerSecond"/> by default. The host's own
        /// sends pass no rate limit of the session, so the share keeps to this pace to leave room
        /// for the session's other messages.
        /// </summary>
        public int ShareBytesPerSecond { get; set; }

        /// <summary>
        /// How long one side of a share waits for the other, in seconds, before it ends the share
        /// with <see cref="SaveShareFailureReason.Timeout"/>: the host for the answer to its offer
        /// and for the receiving player's word that it stored the save, the receiving player for its
        /// game's answer to the offer and for the next part of the save.
        /// <see cref="SessionSaveManager.DefaultShareTimeoutSeconds"/> by default.
        /// </summary>
        public float ShareTimeoutSeconds { get; set; }



        /// <summary>
        /// Registers a section that every player has their own of. Refuses an invalid key, one
        /// longer than <see cref="SessionSaveKeys.MaxPlayerSectionKeyLength"/>, one already
        /// registered, or a version below 1, as <see cref="ISaveManager.RegisterSection"/> does.
        /// </summary>
        public SaveResult RegisterPlayerSection(IPlayerSaveSection section);
        public bool UnregisterPlayerSection(string key);
        public bool IsPlayerSectionRegistered(string key);

        /// <summary>
        /// Saves the session into the slot: every section registered with <see cref="Saves"/>,
        /// and every player section for each player of the session, those whose slot the host
        /// keeps while they are away included. A player the slot holds who is not in the session
        /// keeps their sections in it. The slot's metadata names this host and, when given,
        /// <paramref name="sessionName"/>, up to <see cref="SaveLimits.MaxMetadataValueLength"/>
        /// characters; a null name keeps the one the slot holds. Host only. The metadata is written
        /// after the sections, so a storage failure there answers
        /// <see cref="SessionSaveFailureReason.SaveFailed"/> with the sections already saved.
        /// </summary>
        public SessionSaveResult Save(string slotId, string sessionName = null);

        /// <summary>
        /// Loads the session from the slot, as <see cref="ISaveManager.Load(string)"/> does: every
        /// section registered with <see cref="Saves"/>, and every player section for each player of
        /// the session. A player the slot holds takes back their sections, a player it does not
        /// hold, such as one who joins the saved session for the first time, starts from
        /// <see cref="IPlayerSaveSection.RestoreDefault"/>, and the sections of a player the slot
        /// holds who is not in the session stay in the slot. Host only; load before the game
        /// starts, and use <see cref="LoadPlayer"/> for a player who joins later.
        /// </summary>
        public SessionSaveResult Load(string slotId);

        /// <summary>
        /// Loads one player's sections from the slot, such as for a player who joins a game
        /// already under way: each player section the slot holds for them restores, and each it
        /// does not hold starts from <see cref="IPlayerSaveSection.RestoreDefault"/>. Each section
        /// loads on its own, so a failure leaves the sections before it restored. Host only.
        /// </summary>
        public SessionSaveResult LoadPlayer(string slotId, PlayerId player);

        /// <summary>
        /// Reads the slot as a session save without restoring it; false for a slot that does
        /// not exist or cannot be read, or that <see cref="Save"/> did not write.
        /// </summary>
        public bool TryGetSessionInfo(string slotId, out SessionSaveInfo info);

        /// <summary>
        /// Offers the slot to another player of the session and, once their game accepts, sends
        /// it to them in parts of at most <see cref="ShareChunkSize"/> bytes, at
        /// <see cref="ShareBytesPerSecond"/>. The receiving player checks the whole save before it
        /// stores it, as <see cref="ISaveManager.ImportSlot"/> does, so damaged bytes are never
        /// stored. Both players hear how it goes through <see cref="ISessionSaveManagerEventBus"/>.
        /// Host only, one share to each player at a time.
        /// </summary>
        public SessionSaveResult Share(string slotId, PlayerId player, out uint shareId);

        /// <summary>
        /// Accepts the offer <see cref="ISessionSaveManagerEventBus.OnShareOffered"/> raised: the
        /// save is stored as <paramref name="slotId"/> once it has all arrived, replacing a slot of
        /// that ID, so pick one that none of this player's own saves uses. A new offer from the same
        /// host ends the share it replaces with <see cref="SaveShareFailureReason.Cancelled"/>, since
        /// the host runs one share to a player at a time.
        /// </summary>
        public SessionSaveResult AcceptShare(uint shareId, string slotId);

        /// <summary>Declines the offer <see cref="ISessionSaveManagerEventBus.OnShareOffered"/> raised.</summary>
        public SessionSaveResult DeclineShare(uint shareId);

        /// <summary>
        /// Ends a share this player sends or receives, with
        /// <see cref="SaveShareFailureReason.Cancelled"/> on both sides. False when no such share runs.
        /// </summary>
        public bool CancelShare(uint shareId);

        /// <summary>Fills <paramref name="results"/>, which it clears first, with the shares running on this player.</summary>
        public void GetShares(List<SaveShareTransfer> results);
    }
}
