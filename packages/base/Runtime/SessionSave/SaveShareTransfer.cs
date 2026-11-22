using Mu3Library.Multiplayer.Data;

namespace Mu3Library.SessionSave
{
    /// <summary>
    /// Where one share of a session save stands, on the host that sends it or on the player who
    /// receives it.
    /// </summary>
    public readonly struct SaveShareTransfer
    {
        public uint ShareId { get; }

        /// <summary>The other player: the receiving player on the host, the host on the receiving player.</summary>
        public PlayerId Player { get; }

        /// <summary>True on the host, which sends the save.</summary>
        public bool IsSending { get; }

        /// <summary>
        /// On the host, the slot it shares; on the receiving player, the slot it stores the save
        /// as, or null until it accepts.
        /// </summary>
        public string SlotId { get; }

        /// <summary>Bytes sent so far on the host, received so far on the receiving player.</summary>
        public int BytesTransferred { get; }

        public int TotalBytes { get; }



        public SaveShareTransfer(uint shareId, PlayerId player, bool isSending, string slotId, int bytesTransferred, int totalBytes)
        {
            ShareId = shareId;
            Player = player;
            IsSending = isSending;
            SlotId = slotId;
            BytesTransferred = bytesTransferred;
            TotalBytes = totalBytes;
        }

        public override string ToString() =>
            $"Share {ShareId} {(IsSending ? "to" : "from")} {Player}: {BytesTransferred}/{TotalBytes} bytes";
    }
}
