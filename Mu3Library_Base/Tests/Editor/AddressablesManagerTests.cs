#if MU3LIBRARY_ADDRESSABLES_SUPPORT && MU3LIBRARY_UNITASK_SUPPORT
using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Mu3Library.Addressable;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.TestTools;

namespace Mu3Library.Tests
{
    /// <summary>
    /// Catalog checks and updates against the project's own catalog, which has no remote catalog and
    /// <br/> so nothing to update. That path finishes without a download, so it shows that the calls
    /// <br/> complete and handle each Addressables handle once, not that a real update works.
    /// </summary>
    public class AddressablesManagerTests
    {
        private const double MaxSeconds = 20.0;
        private const int SettleFrames = 5;

        private AddressablesManager _manager;
        private readonly List<string> _handleErrors = new();

        [SetUp]
        public void SetUp()
        {
            _manager = new AddressablesManager();
            Application.logMessageReceived += CollectHandleErrors;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= CollectHandleErrors;
            _manager.Dispose();
            _handleErrors.Clear();

            LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>
        /// Addressables reports a handle read after its release, or released once too often, with
        /// <br/> these messages.
        /// </summary>
        private void CollectHandleErrors(string condition, string stackTrace, LogType type)
        {
            if (condition.Contains("invalid operation handle", StringComparison.Ordinal) ||
                condition.Contains("reference count", StringComparison.Ordinal))
            {
                _handleErrors.Add(condition);
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
        public IEnumerator CatalogUpdatesAsync_NothingToUpdate_CompleteAndHandleEachHandleOnce([Values(true, false)] bool autoReleaseHandle)
        {
            // With nothing to update, Addressables fails the update with an error of its own.
            LogAssert.ignoreFailingMessages = true;

            UniTask<IList<string>> check = _manager.CheckForCatalogUpdatesAsync(autoReleaseHandle);
            yield return RunUntil(() => check.Status != UniTaskStatus.Pending, "CheckForCatalogUpdatesAsync completed");
            check.GetAwaiter().GetResult();

            UniTask<IList<IResourceLocator>> update = _manager.UpdateCatalogsAsync(autoReleaseHandle);
            yield return RunUntil(() => update.Status != UniTaskStatus.Pending, "UpdateCatalogsAsync completed");
            update.GetAwaiter().GetResult();

            // A second release or a late read would surface while Addressables settles its callbacks.
            yield return RunFrames(SettleFrames);

            CollectionAssert.IsEmpty(_handleErrors, "No handle was read after its release or released twice.");
        }
    }
}
#endif
