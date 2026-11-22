using Mu3Library.Multiplayer.Data;

namespace Mu3Library.SessionSave
{
    /// <summary>
    /// A session save the host offers this player. Answer it with
    /// <see cref="ISessionSaveManager.AcceptShare"/> or <see cref="ISessionSaveManager.DeclineShare"/>
    /// within <see cref="SessionSaveManager.ShareTimeoutSeconds"/>.
    /// </summary>
    public readonly struct SaveShareOffer
    {
        public uint ShareId { get; }

        /// <summary>The host who offers the save.</summary>
        public PlayerId From { get; }

        /// <summary>The host's name as the session's roster showed it when the offer arrived.</summary>
        public string FromName { get; }

        /// <summary>The save's size, in bytes.</summary>
        public int Size { get; }

        /// <summary>
        /// The ID of the host's slot. A slot of that ID on this player may be a save of their own,
        /// which <see cref="ISessionSaveManager.AcceptShare"/> into it would replace.
        /// </summary>
        public string SourceSlotId { get; }

        /// <summary>The session name the host saved the slot with, or null when it gave none.</summary>
        public string SessionName { get; }



        public SaveShareOffer(uint shareId, PlayerId from, string fromName, int size, string sourceSlotId, string sessionName)
        {
            ShareId = shareId;
            From = from;
            FromName = fromName ?? string.Empty;
            Size = size;
            SourceSlotId = sourceSlotId;
            SessionName = sessionName;
        }

        public override string ToString() => $"Share {ShareId} from {FromName} ({From}): {SourceSlotId}, {Size} bytes";
    }
}
