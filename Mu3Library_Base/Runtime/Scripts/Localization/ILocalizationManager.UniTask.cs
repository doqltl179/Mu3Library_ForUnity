#if MU3LIBRARY_LOCALIZATION_SUPPORT && MU3LIBRARY_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
using UnityEngine.Localization;

namespace Mu3Library.Localization
{
    public partial interface ILocalizationManager
    {
        public bool IsLocaleChanging { get; }



        public UniTask InitializeAsync();

        public UniTask<string> GetStringAsync(string tableName, string key);
        public UniTask<Locale> GetSelectedLocaleAsync();

        public UniTask ChangeLocaleToNativeAsync();
        public UniTask ChangeLocaleWithEnglishNameAsync(string englishName);
        public UniTask ChangeLocaleAsync(Locale locale);
        public void CancelChangeLocale();
    }
}
#endif
