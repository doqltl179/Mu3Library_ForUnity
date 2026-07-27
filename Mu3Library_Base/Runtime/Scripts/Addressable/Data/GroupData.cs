#if MU3LIBRARY_ADDRESSABLES_SUPPORT

using System.Collections.Generic;

namespace Mu3Library.Addressable.Data
{
    public abstract class GroupData
    {
        public string Name { get; }
        public IReadOnlyList<EntryData> Entries { get; }
        public IReadOnlyList<EntryData> All => Entries;
        public IReadOnlyList<string> Labels { get; }

        protected GroupData(
            string name,
            IReadOnlyList<EntryData> entries,
            IReadOnlyList<string> labels)
        {
            Name = name;
            Entries = entries ?? System.Array.Empty<EntryData>();
            Labels = labels ?? System.Array.Empty<string>();
        }
    }
}

#endif
