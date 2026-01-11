#if MU3LIBRARY_LOCALIZATION_SUPPORT
using System;
using System.Collections.Generic;
using UnityEngine.Localization;

namespace Mu3Library.Localization
{
    public interface ILocalizationManager
    {
        public bool IsInitialized { get; }
        public Locale CurrentLocale { get; }

        public void Initialize(Action callback = null);

        public void GetStringAsync(string tableName, string key, Action<string> callback);
        public string GetString(string tableName, string key);

        public void ChangeLocaleToNative();
        public void ChangeLocaleWithEnglishName(string englishName);
        public void ChangeLocale(Locale locale);
        public List<Locale> GetAvailableLocales();

        public void RemoveLocaleChangedEvent(Action<Locale> action);
        public void AddLocaleChangedEvent(Action<Locale> action);
    }
}
#endif
