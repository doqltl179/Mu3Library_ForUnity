#if MU3LIBRARY_LOCALIZATION_SUPPORT
using System;
using System.Collections.Generic;
using Mu3Library.Localization.Data;
using UnityEngine.Localization;

namespace Mu3Library.Localization
{
    public partial interface ILocalizationManager
    {
        public bool IsInitialized { get; }
        public bool IsInitializing { get; }
        public string InitializeError { get; }
        public Locale CurrentLocale { get; }

        public void Initialize(Action callback = null);
        public void InitializeWithResult(Action<bool, string> callback);

        public string GetString(string tableName, string key);
        public void GetString(string tableName, string key, Action<string> callback);
        /// <summary>
        /// `EntryData` can be obtained from the generated code by LocalizationDataExporter.
        /// </summary>
        public string GetString(EntryData entryData);
        /// <summary>
        /// `EntryData` can be obtained from the generated code by LocalizationDataExporter.
        /// </summary>
        public void GetString(EntryData entryData, Action<string> callback);

        public IReadOnlyList<string> GetAllKeys(string tableName);

        public void ChangeLocaleToNative();
        public void ChangeLocaleWithEnglishName(string englishName);
        public void ChangeLocale(Locale locale);

        public void GetSelectedLocale(Action<Locale> callback);
        public IReadOnlyList<Locale> GetAvailableLocales();
    }
}
#endif
