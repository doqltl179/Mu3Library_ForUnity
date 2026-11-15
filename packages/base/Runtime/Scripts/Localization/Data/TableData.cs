#if MU3LIBRARY_LOCALIZATION_SUPPORT
using System.Collections.Generic;

namespace Mu3Library.Localization.Data
{
    public class TableData
    {
        public string Name { get; }
        public IReadOnlyDictionary<string, LocaleData> Locales { get; }
        public IReadOnlyDictionary<string, EntryData> Entries { get; }

        public TableData(string name, IReadOnlyDictionary<string, LocaleData> locales, IReadOnlyDictionary<string, EntryData> entries)
        {
            Name = name;
            Locales = locales;
            Entries = entries;
        }
    }
}
#endif
