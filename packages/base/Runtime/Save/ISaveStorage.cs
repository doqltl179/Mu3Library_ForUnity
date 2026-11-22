using System.Collections.Generic;

namespace Mu3Library.Save
{
    /// <summary>
    /// Where <see cref="SaveManager"/> keeps its slots: one block of bytes per slot ID, which the
    /// manager has already checked and framed. <see cref="FileSaveStorage"/> keeps them in files and
    /// <see cref="MemorySaveStorage"/> in memory; implement this interface for another place, such as a
    /// platform's cloud storage. A call reports a failure through <see cref="SaveResult"/> with
    /// <see cref="SaveFailureReason.StorageFailed"/> or <see cref="SaveFailureReason.SlotNotFound"/>
    /// instead of throwing.
    /// </summary>
    public interface ISaveStorage
    {
        public bool Exists(string slotId);

        /// <summary>
        /// Reads the slot's bytes, or answers <see cref="SaveFailureReason.SlotNotFound"/>.
        /// </summary>
        public SaveResult Read(string slotId, out byte[] data);

        /// <summary>
        /// Replaces the slot's bytes as a whole. A write that stops partway, such as when the game
        /// quits, should leave the slot's previous bytes readable.
        /// </summary>
        public SaveResult Write(string slotId, byte[] data);

        /// <summary>
        /// Deletes the slot. Deleting a slot that does not exist succeeds.
        /// </summary>
        public SaveResult Delete(string slotId);

        /// <summary>
        /// Adds the ID of every slot the storage holds to <paramref name="results"/>.
        /// </summary>
        public void GetSlotIds(List<string> results);
    }
}
