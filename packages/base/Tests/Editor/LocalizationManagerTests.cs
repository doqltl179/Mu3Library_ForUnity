#if MU3LIBRARY_LOCALIZATION_SUPPORT && MU3LIBRARY_UNITASK_SUPPORT
using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Mu3Library.Localization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Mu3Library.Tests
{
    /// <summary>
    /// Locale changes against Localization settings of the test's own, put in place of the project's
    /// <br/> for the test. Their string database holds the table preload of each initialization open
    /// <br/> until the test lets it finish, which is what a project that preloads tables waits for.
    /// <br/> The editor preference the package keeps the edit-mode locale in is put back afterwards.
    /// </summary>
    public class LocalizationManagerTests
    {
        private const double MaxSeconds = 10.0;
        private const int PendingFrames = 5;
        private const string EditorLocalePreference = "com.unity.localization-edit-locale";

        private LocalizationSettings _originalSettings;
        private bool _hadEditorLocalePreference;
        private string _editorLocalePreference;

        private LocalizationSettings _settings;
        private GatedStringDatabase _stringDatabase;
        private Locale _english;
        private Locale _japanese;
        private LocalizationManager _manager;
        private readonly List<Locale> _announced = new();

        private sealed class TestLocalesProvider : ILocalesProvider
        {
            public List<Locale> Locales { get; } = new();

            public Locale GetLocale(LocaleIdentifier id) => Locales.Find(locale => locale.Identifier == id);

            public void AddLocale(Locale locale) => Locales.Add(locale);

            public bool RemoveLocale(Locale locale) => Locales.Remove(locale);
        }

        private sealed class PendingOperation : AsyncOperationBase<object>
        {
            protected override void Execute()
            {
            }

            public void Finish() => Complete(null, true, (string)null);
        }

        /// <summary>
        /// Takes the preload over from the base database, which completes at once outside play mode.
        /// </summary>
        private sealed class GatedStringDatabase : LocalizedStringDatabase, IPreloadRequired
        {
            private PendingOperation _pending;
            private AsyncOperationHandle _preload;

            public bool IsHeld => _pending != null;

            AsyncOperationHandle IPreloadRequired.PreloadOperation
            {
                get
                {
                    if (!_preload.IsValid())
                    {
                        _preload = Addressables.ResourceManager.CreateCompletedOperation<object>(null, null);
                    }

                    return _preload;
                }
            }

            public void Hold()
            {
                Release();
                _pending = new PendingOperation();
                _preload = Addressables.ResourceManager.StartOperation(_pending, default);
            }

            public void Finish()
            {
                PendingOperation pending = _pending;
                _pending = null;
                pending?.Finish();
            }

            public void Release()
            {
                Finish();
                if (_preload.IsValid())
                {
                    Addressables.Release(_preload);
                }

                _preload = default;
            }
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _hadEditorLocalePreference = EditorPrefs.HasKey(EditorLocalePreference);
            _editorLocalePreference = EditorPrefs.GetString(EditorLocalePreference, string.Empty);
            _originalSettings = LocalizationSettings.HasSettings ? LocalizationSettings.Instance : null;

            _english = Locale.CreateLocale("en");
            _japanese = Locale.CreateLocale("ja");
            TestLocalesProvider locales = new TestLocalesProvider();
            locales.AddLocale(_english);
            locales.AddLocale(_japanese);

            _stringDatabase = new GatedStringDatabase();
            _settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            _settings.SetAvailableLocales(locales);
            _settings.SetStringDatabase(_stringDatabase);
            LocalizationSettings.Instance = _settings;

            _manager = new LocalizationManager();
            UniTask initialize = _manager.InitializeAsync();
            yield return RunUntil(() => initialize.Status != UniTaskStatus.Pending, "the manager initialized");
            initialize.GetAwaiter().GetResult();

            UniTask selectEnglish = _manager.ChangeLocaleAsync(_english);
            yield return RunUntil(() => selectEnglish.Status != UniTaskStatus.Pending, "English was selected");
            selectEnglish.GetAwaiter().GetResult();
            Assert.AreSame(_english, _manager.CurrentLocale);

            _manager.OnLocaleChanged += _announced.Add;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_stringDatabase != null && _stringDatabase.IsHeld)
            {
                _stringDatabase.Finish();

                // No assertion here: the project's settings and the editor preference are put back
                // below whatever state the test left.
                double start = EditorApplication.timeSinceStartup;
                while (!LocalizationSettings.InitializationOperation.IsDone && EditorApplication.timeSinceStartup - start < MaxSeconds)
                {
                    EditorApplication.QueuePlayerLoopUpdate();
                    yield return null;
                }
            }

            try
            {
                _manager?.Dispose();
            }
            finally
            {
                _manager = null;
                _announced.Clear();
                RestoreGlobalState();
            }
        }

        private void RestoreGlobalState()
        {
            LocalizationSettings.Instance = _originalSettings;

            try
            {
                if (_settings != null)
                {
                    ((IDisposable)_settings).Dispose();
                    Object.DestroyImmediate(_settings);
                }

                _stringDatabase?.Release();

                if (_english != null)
                {
                    Object.DestroyImmediate(_english);
                }

                if (_japanese != null)
                {
                    Object.DestroyImmediate(_japanese);
                }
            }
            finally
            {
                _settings = null;
                _stringDatabase = null;
                _english = null;
                _japanese = null;

                if (_hadEditorLocalePreference)
                {
                    EditorPrefs.SetString(EditorLocalePreference, _editorLocalePreference);
                }
                else
                {
                    EditorPrefs.DeleteKey(EditorLocalePreference);
                }
            }
        }

        private static IEnumerator RunUntil(Func<bool> condition, string description)
        {
            double start = EditorApplication.timeSinceStartup;
            while (!condition() && EditorApplication.timeSinceStartup - start < MaxSeconds)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
            }

            Assert.IsTrue(condition(), $"Waited {MaxSeconds} seconds until {description}.");
        }

        private static IEnumerator RunFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ChangeLocaleAsync_WhileTheTablesPreload_CompletesOnceTheChangeIsAnnounced()
        {
            _stringDatabase.Hold();

            UniTask change = _manager.ChangeLocaleAsync(_japanese);
            yield return RunFrames(PendingFrames);

            Assert.AreEqual(UniTaskStatus.Pending, change.Status, "The change waits for the table preload.");
            Assert.IsTrue(_manager.IsLocaleChanging);
            CollectionAssert.DoesNotContain(_announced, _japanese, "The package announces the change after the preload.");

            _stringDatabase.Finish();
            yield return RunUntil(() => change.Status != UniTaskStatus.Pending, "ChangeLocaleAsync completed");
            change.GetAwaiter().GetResult();

            CollectionAssert.Contains(_announced, _japanese, "OnLocaleChanged has been raised by the time the change completes.");
            Assert.IsFalse(_manager.IsLocaleChanging);
            Assert.IsTrue(LocalizationSettings.InitializationOperation.IsDone, "The initialization for the new locale is over.");
            Assert.AreSame(_japanese, _manager.CurrentLocale);
        }

        [UnityTest]
        public IEnumerator CancelChangeLocale_DuringTheWait_EndsTheWaitAndKeepsTheSelection()
        {
            _stringDatabase.Hold();

            UniTask change = _manager.ChangeLocaleAsync(_japanese);
            yield return RunFrames(PendingFrames);
            Assert.AreEqual(UniTaskStatus.Pending, change.Status, "The change waits for the table preload.");

            _manager.CancelChangeLocale();
            Assert.IsFalse(_manager.IsLocaleChanging);

            yield return RunUntil(() => change.Status != UniTaskStatus.Pending, "ChangeLocaleAsync completed");
            change.GetAwaiter().GetResult();
            CollectionAssert.DoesNotContain(_announced, _japanese, "Canceling ends the wait, not the package's initialization.");

            _stringDatabase.Finish();
            yield return RunUntil(() => _announced.Contains(_japanese), "the change was announced");
            Assert.AreSame(_japanese, _manager.CurrentLocale, "The selection stays.");
        }

        [UnityTest]
        public IEnumerator ChangeLocaleAsync_SameLocaleWhileTheFirstCallWaits_WaitsForTheChangeToFinish()
        {
            _stringDatabase.Hold();

            UniTask first = _manager.ChangeLocaleAsync(_japanese);
            yield return RunFrames(PendingFrames);
            UniTask second = _manager.ChangeLocaleAsync(_japanese);
            yield return RunFrames(PendingFrames);

            Assert.AreNotEqual(UniTaskStatus.Pending, first.Status, "The newer call ended the first one's wait.");
            Assert.AreEqual(UniTaskStatus.Pending, second.Status, "The package ignores the second selection, and the change the first one started is still under way.");
            Assert.IsTrue(_manager.IsLocaleChanging);

            _stringDatabase.Finish();
            yield return RunUntil(() => second.Status != UniTaskStatus.Pending, "the second ChangeLocaleAsync completed");
            first.GetAwaiter().GetResult();
            second.GetAwaiter().GetResult();

            CollectionAssert.Contains(_announced, _japanese, "OnLocaleChanged has been raised by the time the second call completes.");
            Assert.IsFalse(_manager.IsLocaleChanging);
            Assert.IsTrue(LocalizationSettings.InitializationOperation.IsDone);
        }

        [UnityTest]
        public IEnumerator ChangeLocaleAsync_LocaleAlreadySelected_CompletesAtOnce()
        {
            _stringDatabase.Hold();

            UniTask change = _manager.ChangeLocaleAsync(_english);

            Assert.AreEqual(UniTaskStatus.Succeeded, change.Status, "Nothing restarts, so there is nothing to wait for.");
            Assert.IsFalse(_manager.IsLocaleChanging);
            Assert.IsEmpty(_announced);
            yield return null;
        }
    }
}
#endif
