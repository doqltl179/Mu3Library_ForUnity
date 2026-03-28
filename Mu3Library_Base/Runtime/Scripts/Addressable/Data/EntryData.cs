#if MU3LIBRARY_ADDRESSABLES_SUPPORT

using System.Collections.Generic;

namespace Mu3Library.Addressable.Data
{
    public sealed class EntryData
    {
        public string GroupName { get; }
        public string Name { get; }
        public string Address { get; }
        public IReadOnlyList<EntryData> SubEntries { get; }

        public EntryData(string groupName, string name, string address, IReadOnlyList<EntryData> subEntries = null)
        {
            GroupName = groupName;
            Name = name;
            Address = address;
            SubEntries = subEntries ?? System.Array.Empty<EntryData>();
        }
    }
}

#endif