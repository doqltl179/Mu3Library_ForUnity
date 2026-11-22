using System.Collections.Generic;

namespace Mu3Library.Save
{
    /// <summary>
    /// Saves and loads the registered sections into named slots of a storage. A slot holds one
    /// section per key; saving one section keeps the others, and a section the slot holds but that is
    /// not registered stays in it untouched. Calls run on the calling thread and report the outcome
    /// through <see cref="SaveResult"/> instead of throwing.
    /// </summary>
    public interface ISaveManager
    {
        public ISaveStorage Storage { get; }

        /// <summary>
        /// Largest slot a save writes or an import accepts, in bytes, all sections together;
        /// a larger one fails with <see cref="SaveFailureReason.TooLarge"/>.
        /// </summary>
        public int MaxSlotSize { get; set; }



        /// <summary>
        /// Registers a section, refusing an invalid key or one already registered.
        /// </summary>
        public SaveResult RegisterSection(ISaveSection section);
        public bool UnregisterSection(string key);
        public bool IsSectionRegistered(string key);

        /// <summary>
        /// Captures every registered section and writes them into the slot, creating it if needed.
        /// Nothing is written if any section fails to capture or serialize.
        /// </summary>
        public SaveResult Save(string slotId);

        /// <summary>
        /// Captures one registered section and writes it into the slot, keeping its other sections.
        /// </summary>
        public SaveResult Save(string slotId, string sectionKey);

        /// <summary>
        /// Restores every registered section the slot holds, and calls
        /// <see cref="ISaveSection.RestoreDefault"/> on each registered section it does not hold.
        /// Every section is read and checked first, so a damaged or too new section restores none.
        /// </summary>
        public SaveResult Load(string slotId);

        /// <summary>
        /// Restores one registered section from the slot.
        /// </summary>
        public SaveResult Load(string slotId, string sectionKey);

        /// <summary>
        /// Reads a section's data from the slot without restoring it into the registered section,
        /// such as for a slot list that shows a summary, and without migrating it.
        /// </summary>
        public SaveResult Peek<TData>(string slotId, string sectionKey, out TData data, out int savedVersion);

        public bool HasSlot(string slotId);

        /// <summary>
        /// Reads what the slot holds. Answers false for a slot that does not exist or cannot be read.
        /// </summary>
        public bool TryGetSlotInfo(string slotId, out SaveSlotInfo info);

        /// <summary>
        /// Adds every slot of the storage that can be read to <paramref name="results"/>.
        /// </summary>
        public void GetSlots(List<SaveSlotInfo> results);

        public SaveResult DeleteSlot(string slotId);

        /// <summary>
        /// Removes one section from the slot and keeps the others.
        /// </summary>
        public SaveResult DeleteSection(string slotId, string sectionKey);

        /// <summary>
        /// Hands out the slot's bytes, checked, such as to back it up or send it elsewhere.
        /// </summary>
        public SaveResult ExportSlot(string slotId, out byte[] data);

        /// <summary>
        /// Checks bytes that <see cref="ExportSlot"/> handed out and writes them as the slot
        /// <paramref name="slotId"/>, replacing a slot of that ID.
        /// </summary>
        public SaveResult ImportSlot(string slotId, byte[] data);
    }
}
