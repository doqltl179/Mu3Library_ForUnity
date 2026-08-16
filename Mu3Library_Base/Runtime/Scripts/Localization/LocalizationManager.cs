#if MU3LIBRARY_LOCALIZATION_SUPPORT
using System;
using System.Collections.Generic;
using System.Globalization;
using Mu3Library.DI;
using Mu3Library.Foundation.Event;
using Mu3Library.Localization.Data;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Mu3Library.Localization
{
    public partial class LocalizationManager : ILocalizationManager, ILocalizationManagerEventBus, IUpdatable, IDisposable
    {
        private bool _isInitialized = false;
        public bool IsInitialized => _isInitialized;

        private string _initializeError = string.Empty;
        public string InitializeError => _initializeError;

        private bool _isInitializing = false;
        public bool IsInitializing => _isInitializing;

        private Locale m_defaultLocale;
        private Locale _defaultLocale
        {
            get
            {
                if (m_defaultLocale == null)
                {
                    m_defaultLocale = FindDefaultLocale() ?? _fallbackLocale;
                }

                return m_defaultLocale;
            }
        }

        private Locale m_fallbackLocale;
        /// <summary>
        /// The locale this manager creates and owns when the project settings carry none.
        /// <br/> There is at most one of it for the manager's whole life, and it is destroyed with it.
        /// </summary>
        private Locale _fallbackLocale
        {
            get
            {
                if (m_fallbackLocale == null)
                {
                    m_fallbackLocale = CreateEnglishFallbackLocale();
                }

                return m_fallbackLocale;
            }
        }

        private Locale _currentLocale;
        public Locale CurrentLocale
        {
            get
            {
                if (_currentLocale == null)
                {
                    return _defaultLocale;
                }

                return _currentLocale;
            }
        }

        private float _lastInitializeProgress = -1.0f;
        private AsyncOperationHandle<LocalizationSettings> _initializeHandle;
        private readonly SubscribeHandler _subscribeHandler = new();

        private bool _isSelectedLocaleChangedHooked = false;
        private Action<Locale> _onLocaleChanged;

        public event Action OnInitialized;
        public event Action<bool, string> OnInitializeResult;
        public event Action<float> OnInitializeProgress;

        /// <summary>
        /// Subscribing attaches this manager to the Localization package once, so a locale change
        /// <br/> made anywhere reaches every subscriber and keeps <see cref="CurrentLocale"/> current.
        /// </summary>
        public event Action<Locale> OnLocaleChanged
        {
            add
            {
                EnsureSelectedLocaleChangedHook();
                _onLocaleChanged += value;
            }
            remove => _onLocaleChanged -= value;
        }



        public void Dispose()
        {
            _subscribeHandler.Dispose();

            // The initialization operation belongs to the Localization package, so only the
            // handler this manager attached is taken back off it.
            if (_initializeHandle.IsValid())
            {
                _initializeHandle.Completed -= OnInitializeCompleted;
            }

            DisposeSelectedLocaleChangedHook();
            DisposeLocaleChange();

            _isInitialized = false;
            _isInitializing = false;
            _lastInitializeProgress = -1.0f;
            _initializeError = string.Empty;

            m_defaultLocale = null;
            _currentLocale = null;

            DestroyFallbackLocale();

            OnInitialized = null;
            OnInitializeResult = null;
            OnInitializeProgress = null;
            _onLocaleChanged = null;
        }

        private void DestroyFallbackLocale()
        {
            if (m_fallbackLocale == null)
            {
                return;
            }

            // Only the locale this manager created is destroyed. A locale that came from the
            // project settings is an asset and is never owned here.
            Locale locale = m_fallbackLocale;
            m_fallbackLocale = null;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(locale);
                return;
            }
#endif

            UnityEngine.Object.Destroy(locale);
        }

        /// <summary>
        /// Implemented by the UniTask part, which owns the locale change cancellation.
        /// </summary>
        partial void DisposeLocaleChange();

        public void Update()
        {
            UpdateInitializeProgress();
        }

        #region Initialize
        public void Initialize(Action callback = null)
        {
            if (_isInitialized)
            {
                OnInitializeProgress?.Invoke(1.0f);
                callback?.Invoke();
                return;
            }

            // The callback belongs to this initialize call only, so it goes through the
            // one-shot subscription instead of staying on the event for every later one.
            SubscribeOnInitializedOnce(callback);

            BeginInitialize();
        }

        public void InitializeWithResult(Action<bool, string> callback)
        {
            if (_isInitialized)
            {
                OnInitializeProgress?.Invoke(1.0f);
                callback?.Invoke(true, string.Empty);
                return;
            }

            SubscribeOnInitializeResultOnce(callback);

            BeginInitialize();
        }

        /// <summary>
        /// Starts the one initialization this manager runs. It is safe to call repeatedly:
        /// <br/> a finished or running initialization is left alone.
        /// </summary>
        private void BeginInitialize()
        {
            if (_isInitialized)
            {
                return;
            }

            if (_isInitializing)
            {
                return;
            }

            _isInitializing = true;

            EnsureSelectedLocaleChangedHook();

            _initializeHandle = LocalizationSettings.InitializationOperation;
            if (_initializeHandle.IsDone)
            {
                OnInitializeCompleted(_initializeHandle);
                return;
            }

            _initializeHandle.Completed += OnInitializeCompleted;
        }

        private void OnInitializeCompleted(AsyncOperationHandle<LocalizationSettings> handle)
        {
            handle.Completed -= OnInitializeCompleted;
            bool isSuccess = false;
            string errorMessage = string.Empty;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                _isInitialized = true;
                isSuccess = true;
                errorMessage = string.Empty;
                Debug.Log("Localization initialized.");
            }
            else
            {
                _isInitialized = false;
                isSuccess = false;
                errorMessage = handle.OperationException?.Message ?? "Unknown initialization error.";
                Debug.LogError($"Localization initialize failed.\r\n{errorMessage}");
            }

            LocalizationSettings settings = handle.Result;
            if (settings != null)
            {
                _currentLocale = settings.GetSelectedLocale();
            }

            _isInitializing = false;
            _lastInitializeProgress = handle.PercentComplete;
            _initializeError = errorMessage;
            OnInitializeProgress?.Invoke(_lastInitializeProgress);

            OnInitialized?.Invoke();
            OnInitializeResult?.Invoke(isSuccess, errorMessage);
        }

        private void UpdateInitializeProgress()
        {
            if (!_isInitializing || !_initializeHandle.IsValid())
            {
                return;
            }

            float progress = _initializeHandle.PercentComplete;
            if (Mathf.Approximately(progress, _lastInitializeProgress))
            {
                return;
            }

            _lastInitializeProgress = progress;
            OnInitializeProgress?.Invoke(progress);
        }
        #endregion

        #region String
        public string GetString(string tableName, string key)
        {
            LocalizedStringDatabase stringDatabase = GetStringDatabase();
            StringTable table = stringDatabase != null ? stringDatabase.GetTable(tableName) : null;
            return GetTableEntryValue(table, key);
        }

        public string GetString(EntryData entryData)
        {
            if (entryData == null)
            {
                Debug.LogError("EntryData is null.");
                return string.Empty;
            }

            return GetString(entryData.TableName, entryData.Key);
        }

        public void GetString(string tableName, string key, Action<string> callback)
        {
            LocalizedStringDatabase stringDatabase = GetStringDatabase();
            if (stringDatabase == null)
            {
                callback?.Invoke(string.Empty);
                return;
            }

            AsyncOperationHandle<StringTable> handle = stringDatabase.GetTableAsync(tableName);
            if (handle.IsDone)
            {
                callback?.Invoke(GetTableEntryValue(ResolveTable(handle), key));
                return;
            }

            handle.Completed += operation => callback?.Invoke(GetTableEntryValue(ResolveTable(operation), key));
        }

        public void GetString(EntryData entryData, Action<string> callback)
        {
            if (entryData == null)
            {
                Debug.LogError("EntryData is null.");
                callback?.Invoke(string.Empty);
                return;
            }

            GetString(entryData.TableName, entryData.Key, callback);
        }

        public IReadOnlyList<string> GetAllKeys(string tableName)
        {
            LocalizedStringDatabase stringDatabase = GetStringDatabase();
            StringTable table = stringDatabase != null ? stringDatabase.GetTable(tableName) : null;
            if (table == null)
            {
                return Array.Empty<string>();
            }

            List<string> keys = new(table.Count);
            foreach (StringTableEntry entry in table.Values)
            {
                if (entry != null)
                {
                    keys.Add(entry.Key);
                }
            }

            return keys;
        }

        /// <summary>
        /// Reading any <see cref="LocalizationSettings"/> member creates the settings when the project
        /// <br/> carries none, so the presence check comes before every access.
        /// </summary>
        private static LocalizedStringDatabase GetStringDatabase()
        {
            return LocalizationSettings.HasSettings ? LocalizationSettings.StringDatabase : null;
        }

        private static StringTable ResolveTable(AsyncOperationHandle<StringTable> handle)
        {
            return handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : null;
        }

        private static string GetTableEntryValue(StringTable table, string key)
        {
            StringTableEntry entry = table != null ? table.GetEntry(key) : null;
            return entry != null ? entry.LocalizedValue : string.Empty;
        }
        #endregion

        #region Locale
        public void ChangeLocaleToNative()
        {
            ChangeLocale(FindNativeLocale());
        }

        public void ChangeLocaleWithEnglishName(string englishName)
        {
            ChangeLocale(FindLocaleByEnglishName(englishName));
        }

        public void ChangeLocale(Locale locale)
        {
            SelectLocale(locale);
        }

        /// <summary>
        /// The one place that hands a locale to the Localization package, shared by the callback
        /// <br/> and the UniTask paths so both hook the change event before the selection is made.
        /// </summary>
        private bool SelectLocale(Locale locale)
        {
            if (locale == null)
            {
                return false;
            }

            if (!LocalizationSettings.HasSettings)
            {
                Debug.LogWarning("Localization settings are missing, so the locale is not changed.");
                return false;
            }

            EnsureSelectedLocaleChangedHook();

            LocalizationSettings.SelectedLocale = locale;
            _currentLocale = locale;
            return true;
        }

        public void GetSelectedLocale(Action<Locale> callback)
        {
            if (!LocalizationSettings.HasSettings)
            {
                callback?.Invoke(_defaultLocale);
                return;
            }

            // The property builds the handle on demand, so it is read once and that one handle
            // is what gets waited on.
            AsyncOperationHandle<Locale> handle = LocalizationSettings.SelectedLocaleAsync;
            if (handle.IsDone)
            {
                callback?.Invoke(ResolveSelectedLocale(handle));
                return;
            }

            handle.Completed += operation => callback?.Invoke(ResolveSelectedLocale(operation));
        }

        public IReadOnlyList<Locale> GetAvailableLocales()
        {
            return GetLocales();
        }

        private Locale ResolveSelectedLocale(AsyncOperationHandle<Locale> handle)
        {
            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                return handle.Result;
            }

            return _defaultLocale;
        }

        private static IReadOnlyList<Locale> GetLocales()
        {
            if (!LocalizationSettings.HasSettings)
            {
                return Array.Empty<Locale>();
            }

            IList<Locale> locales = LocalizationSettings.AvailableLocales?.Locales;
            return locales as IReadOnlyList<Locale> ?? Array.Empty<Locale>();
        }

        /// <summary>
        /// <see cref="LocaleIdentifier.CultureInfo"/> stays null for a custom locale code, so every
        /// <br/> lookup that reads the culture checks it first.
        /// </summary>
        private static CultureInfo GetCultureInfo(Locale locale)
        {
            return locale != null ? locale.Identifier.CultureInfo : null;
        }

        private static Locale FindDefaultLocale()
        {
            IReadOnlyList<Locale> locales = GetLocales();

            Locale first = null;
            foreach (Locale locale in locales)
            {
                if (locale == null)
                {
                    continue;
                }

                first ??= locale;

                CultureInfo cultureInfo = GetCultureInfo(locale);
                if (cultureInfo != null && cultureInfo.TwoLetterISOLanguageName == "en")
                {
                    return locale;
                }
            }

            return first;
        }

        private static Locale FindLocaleByEnglishName(string englishName)
        {
            if (string.IsNullOrEmpty(englishName))
            {
                return null;
            }

            foreach (Locale locale in GetLocales())
            {
                CultureInfo cultureInfo = GetCultureInfo(locale);
                if (cultureInfo != null && cultureInfo.EnglishName == englishName)
                {
                    return locale;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves the locale for <see cref="Application.systemLanguage"/>. The identifier the
        /// <br/> Localization package builds from a <see cref="SystemLanguage"/> is matched first, because
        /// <br/> the enum name and the culture's English name disagree for entries such as
        /// <br/> <see cref="SystemLanguage.ChineseSimplified"/>.
        /// </summary>
        private static Locale FindNativeLocale()
        {
            SystemLanguage systemLanguage = Application.systemLanguage;
            LocaleIdentifier nativeIdentifier = new(systemLanguage);
            IReadOnlyList<Locale> locales = GetLocales();

            foreach (Locale locale in locales)
            {
                if (locale != null && locale.Identifier == nativeIdentifier)
                {
                    return locale;
                }
            }

            // Then the same language with any region, so a system set to "en-GB" still finds "en".
            string nativeLanguage = nativeIdentifier.CultureInfo?.TwoLetterISOLanguageName;
            if (!string.IsNullOrEmpty(nativeLanguage))
            {
                foreach (Locale locale in locales)
                {
                    CultureInfo cultureInfo = GetCultureInfo(locale);
                    if (cultureInfo != null && cultureInfo.TwoLetterISOLanguageName == nativeLanguage)
                    {
                        return locale;
                    }
                }
            }

            return FindLocaleByEnglishName(systemLanguage.ToString());
        }

        /// <summary>
        /// Creates the locale behind <see cref="_fallbackLocale"/>. Call it through that property
        /// <br/> so the manager keeps owning exactly one.
        /// </summary>
        private Locale CreateEnglishFallbackLocale()
        {
            Locale locale = Locale.CreateLocale("en");
            if (locale != null)
            {
                return locale;
            }

            locale = ScriptableObject.CreateInstance<Locale>();
            locale.Identifier = new LocaleIdentifier("en");
            return locale;
        }
        #endregion

        #region Event
        public ISubscriptionInfo SubscribeOnInitializedOnce(Action callback)
            => SubscribeOnInitializedOnce(callback, null);

        public ISubscriptionInfo SubscribeOnInitializedOnce(Action callback, Action onDisposed)
            => _subscribeHandler.SubscribeOnce(
                handler => OnInitialized += handler,
                handler => OnInitialized -= handler,
                callback,
                onDisposed);

        public ISubscriptionInfo SubscribeOnInitializeResultOnce(Action<bool, string> callback)
            => SubscribeOnInitializeResultOnce(callback, null);

        public ISubscriptionInfo SubscribeOnInitializeResultOnce(Action<bool, string> callback, Action onDisposed)
            => _subscribeHandler.SubscribeOnce(
                handler => OnInitializeResult += handler,
                handler => OnInitializeResult -= handler,
                callback,
                onDisposed);

        /// <summary>
        /// Attaches this manager to the package's locale change event once. The manager owns that
        /// <br/> subscription, so <see cref="Dispose"/> is what takes it back off.
        /// </summary>
        private void EnsureSelectedLocaleChangedHook()
        {
            if (_isSelectedLocaleChangedHooked || !LocalizationSettings.HasSettings)
            {
                return;
            }

            _isSelectedLocaleChangedHooked = true;
            LocalizationSettings.SelectedLocaleChanged += HandleSelectedLocaleChanged;
        }

        private void DisposeSelectedLocaleChangedHook()
        {
            if (!_isSelectedLocaleChangedHooked)
            {
                return;
            }

            _isSelectedLocaleChangedHooked = false;
            LocalizationSettings.SelectedLocaleChanged -= HandleSelectedLocaleChanged;
        }

        private void HandleSelectedLocaleChanged(Locale locale)
        {
            _currentLocale = locale;
            _onLocaleChanged?.Invoke(locale);
        }
        #endregion
    }
}
#endif
