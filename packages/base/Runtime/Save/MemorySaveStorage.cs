using System.Collections.Generic;

namespace Mu3Library.Save
{
    /// <summary>
    /// Keeps slots in memory for as long as the object lives, such as for tests or for a slot that
    /// only lasts one run.
    /// </summary>
    public class MemorySaveStorage : ISaveStorage
    {
        private readonly Dictionary<string, byte[]> _slots = new();



        public bool Exists(string slotId) => slotId != null && _slots.ContainsKey(slotId);

        public SaveResult Read(string slotId, out byte[] data)
        {
            if (slotId != null && _slots.TryGetValue(slotId, out byte[] stored))
            {
                data = (byte[])stored.Clone();
                return SaveResult.Success;
            }

            data = null;
            return SaveResult.Failure(SaveFailureReason.SlotNotFound);
        }

        public SaveResult Write(string slotId, byte[] data)
        {
            if (slotId == null || data == null)
            {
                return SaveResult.Failure(SaveFailureReason.StorageFailed, message: "No slot ID or data.");
            }

            _slots[slotId] = (byte[])data.Clone();
            return SaveResult.Success;
        }

        public SaveResult Delete(string slotId)
        {
            if (slotId != null)
            {
                _slots.Remove(slotId);
            }

            return SaveResult.Success;
        }

        public void GetSlotIds(List<string> results)
        {
            results.AddRange(_slots.Keys);
        }
    }
}
