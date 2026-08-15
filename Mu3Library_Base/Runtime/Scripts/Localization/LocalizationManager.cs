#if MU3LIBRARY_LOCALIZATION_SUPPORT
using System;
using System.Collections.Generic;
using System.Linq;
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
                    IReadOnlyList<Locale> locales = LocalizationSettings.AvailableLocales?.Locales;
                    Locale fromSettings = locales?
                        .FirstOrDefault(t => t.Identifier.CultureInfo.TwoLetterISOLanguageName == "en");
                    if (fromSettings == null && locales != null && locales.Count > 0)
                    {
                        fromSettings = locales[0];
                    }

                    m_defaultLocale = fromSettings ?? _fallbackLocale;
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

        public event Action OnInitialized;
        public event Action<bool, string> OnInitializeResult;
        public event Action<float> OnInitializeProgress;



        public void Dispose()
        {
            _subscribeHandler.Dispose();

            // The initialization operation belongs to the Localization package, so only the
            // handler this manager attached is taken back off it.
            if (_initializeHandle.IsValid())
            {
                _initializeHandle.Completed -= OnInitializeCompleted;
            }

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

        #region Utility
        public void Update()
        {
            UpdateInitializeProgress();
        }

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

            _initializeHandle = LocalizationSettings.InitializationOperation;
            if (_initializeHandle.IsDone)
            {
                OnInitializeCompleted(_initializeHandle);
                return;
            }

            _initializeHandle.Completed += OnInitializeCompleted;
        }

        public void GetString(string tableName, string key, Action<string> callback)
        {
            LocalizedStringDatabase stringDatabase = LocalizationSettings.StringDatabase;
            if (stringDatabase == null)
            {
                callback?.Invoke("");
                return;
            }

            AsyncOperationHandle<StringTable> handle = stringDatabase.GetTableAsync(tableName);
            handle.Completed += operation =>
            {
                if (operation.Status == AsyncOperationStatus.Succeeded)
                {
                    string value = GetTableEntryValue(operation.Result, key);
                    callback?.Invoke(value);
                }
                else
                {
                    callback?.Invoke("");
                }
            };
        }

        public void GetString(EntryData entryData, Action<string> callback)
        {
            if (entryData == null)
            {
                Debug.LogError("EntryData is null.");
                callback?.Invoke("");
                return;
            }

            GetString(entryData.TableName, entryData.Key, callback);
        }

        public string GetString(string tableName, string key)
        {
            LocalizedStringDatabase sdb = LocalizationSettings.StringDatabase;
            StringTable table = sdb != null ? sdb.GetTable(tableName) : null;
            return GetTableEntryValue(table, key);
        }

        public string GetString(EntryData entryData)
        {
            if (entryData == null)
            {
                Debug.LogError("EntryData is null.");
                return "";
            }

            return GetString(entryData.TableName, entryData.Key);
        }

        public List<string> GetAllKeys(string tableName)
        {
            LocalizedStringDatabase sdb = LocalizationSettings.StringDatabase;
            StringTable table = sdb != null ? sdb.GetTable(tableName) : null;
            if (table == null)
            {
                return new List<string>();
            }

            List<string> keys = new();
            foreach (StringTableEntry entry in table.Values)
            {
                keys.Add(entry.Key);
            }

            return keys;
        }

        public void ChangeLocaleToNative()
        {
            SystemLanguage sl = Application.systemLanguage;
            ChangeLocaleWithEnglishName(sl.ToString());
        }

        public void ChangeLocaleWithEnglishName(string englishName)
        {
            ChangeLocale(FindLocaleByEnglishName(englishName));
        }

        public void ChangeLocale(Locale locale)
        {
            if (locale == null)
            {
                return;
            }

            _currentLocale = locale;
            LocalizationSettings.SelectedLocale = locale;
        }

        private Locale FindLocaleByEnglishName(string englishName)
        {
            return LocalizationSettings.AvailableLocales.Locales
                .FirstOrDefault(locale => locale.Identifier.CultureInfo.EnglishName == englishName);
        }

        private static string GetTableEntryValue(StringTable table, string key)
        {
            StringTableEntry entry = table != null ? table.GetEntry(key) : null;
            return entry != null ? entry.LocalizedValue : "";
        }

        public void GetSelectedLocale(Action<Locale> callback)
        {
            void onCompleted(AsyncOperationHandle<Locale> handle)
            {
                LocalizationSettings.SelectedLocaleAsync.Completed -= onCompleted;

                Locale locale = handle.Status == AsyncOperationStatus.Succeeded
                    ? handle.Result
                    : _defaultLocale;
                callback?.Invoke(locale);
            }

            var handle = LocalizationSettings.SelectedLocaleAsync;
            if (handle.IsDone)
            {
                onCompleted(handle);
                return;
            }

            LocalizationSettings.SelectedLocaleAsync.Completed += onCompleted;
        }

        public List<Locale> GetAvailableLocales()
        {
            return LocalizationSettings.AvailableLocales.Locales;
        }

        public void RemoveLocaleChangedEvent(Action<Locale> action)
        {
            LocalizationSettings.SelectedLocaleChanged -= action;
        }

        public void AddLocaleChangedEvent(Action<Locale> action)
        {
            LocalizationSettings.SelectedLocaleChanged += action;
        }
        #endregion

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

            var settings = handle.Result;
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
    }
}
#endif
