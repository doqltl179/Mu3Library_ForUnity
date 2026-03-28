#if MU3LIBRARY_ADDRESSABLES_SUPPORT

using System.Collections.Generic;

namespace Mu3Library.Addressable.Data
{
    public class GroupData
    {
        public string Name { get; }
        public IReadOnlyDictionary<string, EntryData> Entries { get; }
        public IReadOnlyDictionary<string, LabelData> Labels { get; }

        public GroupData(
            string name,
            IReadOnlyDictionary<string, EntryData> entries,
            IReadOnlyDictionary<string, LabelData> labels)
        {
            Name = name;
            Entries = entries;
            Labels = labels;
        }
    }
}

#endif