#if MU3LIBRARY_LOCALIZATION_SUPPORT && MU3LIBRARY_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
using Mu3Library.Localization.Data;
using UnityEngine.Localization;

namespace Mu3Library.Localization
{
    public partial interface ILocalizationManager
    {
        public bool IsLocaleChanging { get; }



        public UniTask InitializeAsync();

        public UniTask<string> GetStringAsync(string tableName, string key);
        /// <summary>
        /// `EntryData` can be obtained from the generated code by LocalizationDataExporter.
        /// </summary>
        public UniTask<string> GetStringAsync(EntryData entryData);

        /// <summary>
        /// Loads the asset an AssetTable holds for the current locale.
        /// Null answers a missing table, entry, or settings, and a failed load.
        /// </summary>
        public UniTask<T> GetAssetAsync<T>(string tableName, string key) where T : UnityEngine.Object;

        public UniTask<Locale> GetSelectedLocaleAsync();

        public UniTask ChangeLocaleToNativeAsync();
        public UniTask ChangeLocaleWithEnglishNameAsync(string englishName);
        public UniTask ChangeLocaleAsync(Locale locale);
        public void CancelChangeLocale();
    }
}
#endif
