using System;

namespace Mu3Library.Save
{
    public interface ISaveManagerEventBus
    {
        /// <summary>
        /// A save wrote the slot: the slot ID, and the section key, or null when every registered
        /// section was saved.
        /// </summary>
        public event Action<string, string> OnSaved;

        /// <summary>
        /// A load restored from the slot: the slot ID, and the section key, or null when the whole
        /// slot was loaded.
        /// </summary>
        public event Action<string, string> OnLoaded;

        /// <summary>
        /// A slot was deleted.
        /// </summary>
        public event Action<string> OnSlotDeleted;

        /// <summary>
        /// <see cref="ISaveManager.ImportSlot"/> wrote a slot, new or replacing one of the same ID.
        /// </summary>
        public event Action<string> OnSlotImported;
    }
}
