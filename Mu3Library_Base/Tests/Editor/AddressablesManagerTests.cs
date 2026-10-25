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
using UnityEngine.ResourceManagement.AsyncOperations;
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

        private IEnumerator InitializeManager()
        {
            bool isInitialized = false;
            _manager.Initialize(() => isInitialized = true);
            yield return RunUntil(() => isInitialized, "AddressablesManager initialized");
            Assert.IsTrue(_manager.IsInitialized, "Addressables initialized in edit mode.");
        }

        /// <summary>
        /// Addressables returns a chain operation when it has to wait for something before the catalog
        /// <br/> operation can start. Its debug name is the only public trace of that, and the tests read it
        /// <br/> to make sure they ran the path they describe.
        /// </summary>
        private static bool IsChainOperation(AsyncOperationHandle handle)
            => handle.IsValid() && handle.DebugName.StartsWith("ChainOperation", StringComparison.Ordinal);

        /// <summary>
        /// Runs first in the fixture, while this editor process has not initialized Addressables yet,
        /// <br/> so the check waits for the initialization inside a chain operation that Addressables does
        /// <br/> not auto-release. Only one test per process can see that state.
        /// </summary>
        [UnityTest, Order(0)]
        public IEnumerator CheckForCatalogUpdates_BeforeInitialization_LeavesNoValidHandle()
        {
            bool isCompleted = false;
            _manager.CheckForCatalogUpdates(_ => isCompleted = true, autoReleaseHandle: true);
            AsyncOperationHandle handle = _manager.LastCatalogHandle;
            bool isChain = IsChainOperation(handle);

            yield return RunUntil(() => isCompleted, "CheckForCatalogUpdates completed");
            yield return RunFrames(SettleFrames);

            Assert.IsFalse(handle.IsValid(), "The check handle was released once the check finished.");
            CollectionAssert.IsEmpty(_handleErrors, "No handle was read after its release or released twice.");

            if (!isChain)
            {
                Assert.Ignore("Addressables had already started initializing in this editor process, so the check " +
                    "returned its own operation instead of a chain; the handle was still released once.");
            }
        }

        [UnityTest]
        public IEnumerator CheckForCatalogUpdates_AfterInitialization_LeavesNoValidHandle([Values(true, false)] bool autoReleaseHandle)
        {
            yield return InitializeManager();

            bool isCompleted = false;
            _manager.CheckForCatalogUpdates(_ => isCompleted = true, autoReleaseHandle);
            AsyncOperationHandle handle = _manager.LastCatalogHandle;
            Assert.IsFalse(IsChainOperation(handle), "Once initialized, Addressables returns the check operation itself.");

            yield return RunUntil(() => isCompleted, "CheckForCatalogUpdates completed");
            yield return RunFrames(SettleFrames);

            Assert.IsFalse(handle.IsValid(), "The check handle was released once the check finished.");
            CollectionAssert.IsEmpty(_handleErrors, "No handle was read after its release or released twice.");
        }

        /// <summary>
        /// The project's catalog never has an update, so Addressables never knows of one and always checks
        /// <br/> first inside a chain operation, which it does not auto-release.
        /// </summary>
        [UnityTest]
        public IEnumerator UpdateCatalogs_NoUpdateKnown_LeavesNoValidHandle([Values(true, false)] bool autoReleaseHandle)
        {
            yield return InitializeManager();

            // With nothing to update, Addressables fails the update with an error of its own.
            LogAssert.ignoreFailingMessages = true;

            bool isCompleted = false;
            IList<IResourceLocator> locators = new List<IResourceLocator>();
            _manager.UpdateCatalogs(result =>
            {
                locators = result;
                isCompleted = true;
            }, autoReleaseHandle);
            AsyncOperationHandle handle = _manager.LastCatalogHandle;
            Assert.IsTrue(IsChainOperation(handle), "With no catalog update known, Addressables checks first in a chain operation.");

            yield return RunUntil(() => isCompleted, "UpdateCatalogs completed");
            yield return RunFrames(SettleFrames);

            Assert.IsNull(locators, "An update with nothing to update reports a failure.");
            Assert.IsFalse(handle.IsValid(), "The update handle was released once the update finished.");
            CollectionAssert.IsEmpty(_handleErrors, "No handle was read after its release or released twice.");
        }

        [UnityTest]
        public IEnumerator CatalogUpdatesAsync_NothingToUpdate_CompleteAndHandleEachHandleOnce([Values(true, false)] bool autoReleaseHandle)
        {
            // With nothing to update, Addressables fails the update with an error of its own.
            LogAssert.ignoreFailingMessages = true;

            UniTask<IList<string>> check = _manager.CheckForCatalogUpdatesAsync(autoReleaseHandle);
            AsyncOperationHandle checkHandle = _manager.LastCatalogHandle;
            yield return RunUntil(() => check.Status != UniTaskStatus.Pending, "CheckForCatalogUpdatesAsync completed");
            check.GetAwaiter().GetResult();

            UniTask<IList<IResourceLocator>> update = _manager.UpdateCatalogsAsync(autoReleaseHandle);
            AsyncOperationHandle updateHandle = _manager.LastCatalogHandle;
            yield return RunUntil(() => update.Status != UniTaskStatus.Pending, "UpdateCatalogsAsync completed");
            update.GetAwaiter().GetResult();

            // A second release or a late read would surface while Addressables settles its callbacks.
            yield return RunFrames(SettleFrames);

            Assert.IsFalse(checkHandle.IsValid(), "The check handle was released once the check finished.");
            Assert.IsFalse(updateHandle.IsValid(), "The update handle was released once the update finished.");
            CollectionAssert.IsEmpty(_handleErrors, "No handle was read after its release or released twice.");
        }
    }
}
#endif
