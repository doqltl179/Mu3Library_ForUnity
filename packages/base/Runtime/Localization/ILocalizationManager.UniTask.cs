#if MU3LIBRARY_LOCALIZATION_SUPPORT && MU3LIBRARY_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
using Mu3Library.Localization.Data;
using UnityEngine.Localization;

namespace Mu3Library.Localization
{
    public partial interface ILocalizationManager
    {
        /// <summary>
        /// True while a <see cref="ChangeLocaleAsync"/> waits for its locale change to finish.
        /// </summary>
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
        /// <summary>
        /// Selects the locale and completes once the change is over: the Localization package has
        /// <br/> initialized again for the new locale, preloaded tables included, and has raised
        /// <br/> <see cref="ILocalizationManagerEventBus.OnLocaleChanged"/> for it. Selecting the locale
        /// <br/> that is already selected restarts nothing: the call then waits only for an initialization
        /// <br/> or an announcement still under way, such as the one an earlier call for the same locale
        /// <br/> started, and otherwise completes at once. A newer call or <see cref="CancelChangeLocale"/>
        /// <br/> ends the wait early; the selection itself stays.
        /// </summary>
        public UniTask ChangeLocaleAsync(Locale locale);
        public void CancelChangeLocale();
    }
}
#endif
