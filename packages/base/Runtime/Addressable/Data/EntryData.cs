#if MU3LIBRARY_ADDRESSABLES_SUPPORT

using System.Collections.Generic;

namespace Mu3Library.Addressable.Data
{
    public abstract class EntryData
    {
        public string Name { get; }
        public string Address { get; }
        public IReadOnlyList<string> Labels { get; }
        public IReadOnlyList<EntryData> Entries { get; }
        public IReadOnlyList<EntryData> All => Entries;

        protected EntryData(
            string name,
            string address,
            IReadOnlyList<string> labels = null,
            IReadOnlyList<EntryData> entries = null)
        {
            Name = name;
            Address = address;
            Labels = labels ?? System.Array.Empty<string>();
            Entries = entries ?? System.Array.Empty<EntryData>();
        }
    }
}

#endif
