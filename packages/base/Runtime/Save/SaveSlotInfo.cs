using System;
using System.Collections.Generic;

namespace Mu3Library.Save
{
    /// <summary>
    /// What a slot holds, read without restoring any section.
    /// </summary>
    public sealed class SaveSlotInfo
    {
        public string SlotId { get; }
        public DateTime CreatedUtc { get; }
        public DateTime UpdatedUtc { get; }

        /// <summary>
        /// The slot's size in the storage, in bytes.
        /// </summary>
        public int Size { get; }

        public IReadOnlyList<SaveSectionInfo> Sections { get; }



        public SaveSlotInfo(string slotId, DateTime createdUtc, DateTime updatedUtc, int size, IReadOnlyList<SaveSectionInfo> sections)
        {
            SlotId = slotId;
            CreatedUtc = createdUtc;
            UpdatedUtc = updatedUtc;
            Size = size;
            Sections = sections ?? Array.Empty<SaveSectionInfo>();
        }

        public bool HasSection(string key) => TryGetSection(key, out _);

        public bool TryGetSection(string key, out SaveSectionInfo section)
        {
            foreach (SaveSectionInfo candidate in Sections)
            {
                if (candidate.Key == key)
                {
                    section = candidate;
                    return true;
                }
            }

            section = default;
            return false;
        }
    }

    public readonly struct SaveSectionInfo
    {
        public string Key { get; }

        /// <summary>
        /// The data version the section was saved with.
        /// </summary>
        public int Version { get; }

        public DateTime UpdatedUtc { get; }

        /// <summary>
        /// The serialized data's size, in bytes.
        /// </summary>
        public int Size { get; }



        public SaveSectionInfo(string key, int version, DateTime updatedUtc, int size)
        {
            Key = key;
            Version = version;
            UpdatedUtc = updatedUtc;
            Size = size;
        }
    }
}
