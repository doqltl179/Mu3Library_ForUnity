namespace Mu3Library.Multiplayer.Sync
{
    /// <summary>
    /// One piece of game state that an <see cref="IStateSyncManager"/> keeps the same on every
    /// player of a game, such as a character's position or a door's state: an ID, whether it
    /// changed, and how it writes and reads itself, whole or as what changed. What the state holds
    /// and how precisely it writes it is the game's choice; how many bytes a tick sends each player
    /// and when is the manager's. Derive from <see cref="SyncState"/>, or use
    /// <see cref="TransformSync"/> for a transform, rather than implementing this interface directly.
    /// </summary>
    /// <remarks>
    /// Every player registers the same state under the same ID. The player that sends a state,
    /// its owner, writes it; every other player reads it into its own copy. The manager calls
    /// these members on the main thread, from its <c>Update</c> and from the multiplayer manager's,
    /// and catches an exception they throw, which it logs once per state and exception type in a
    /// session.
    /// </remarks>
    public interface ISyncState
    {
        /// <summary>The state's ID, the same on every player and unique among the states of one manager.</summary>
        public uint Id { get; }

        /// <summary>
        /// Whether the state changed since <see cref="ClearDirty"/>. The manager reads it once a
        /// tick on the player that sends the state, before it writes the state that tick.
        /// </summary>
        public bool IsDirty { get; }

        /// <summary>
        /// Writes the whole state, so that <see cref="ReadFull"/> alone brings a copy up to it. The
        /// manager writes it for a player who has no copy yet, who missed a change, or, for an
        /// unreliable state, every <see cref="StateSyncConfig.FullStateIntervalSeconds"/>.
        /// </summary>
        public void WriteFull(MessageWriter writer);

        /// <summary>
        /// Writes what changed since the last <see cref="ClearDirty"/>, so that
        /// <see cref="ReadDelta"/> brings a copy that read every write before it up to the state.
        /// The manager writes it once in a tick where <see cref="IsDirty"/> is true, for every
        /// player that has every change before it; writing the whole state is always correct.
        /// </summary>
        public void WriteDelta(MessageWriter writer);

        /// <summary>Reads what <see cref="WriteFull"/> wrote into this copy.</summary>
        public void ReadFull(ref MessageReader reader, in SyncReadContext context);

        /// <summary>Reads what <see cref="WriteDelta"/> wrote into this copy.</summary>
        public void ReadDelta(ref MessageReader reader, in SyncReadContext context);

        /// <summary>
        /// Called at the end of a tick in which <see cref="IsDirty"/> was true, once the manager
        /// wrote the change for every player it could: a player it could not reach that tick gets
        /// the whole state later, so the change counts as sent either way.
        /// </summary>
        public void ClearDirty();
    }
}
