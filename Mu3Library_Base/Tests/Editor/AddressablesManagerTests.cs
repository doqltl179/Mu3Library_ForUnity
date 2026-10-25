#if MU3LIBRARY_ADDRESSABLES_SUPPORT && MU3LIBRARY_UNITASK_SUPPORT
using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Mu3Library.Addressable;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
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
        private const string FailedInitializationMessage = "Test initialization failed.";

        private AddressablesManager _manager;
        private readonly List<AddressablesManager> _extraManagers = new();
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
            foreach (AddressablesManager manager in _extraManagers)
            {
                manager.Dispose();
            }

            _extraManagers.Clear();
            _handleErrors.Clear();

            LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>
        /// Addressables reports a handle read after its release, or released once too often, with
        /// <br/> these messages. An exception thrown inside an Addressables callback, or out of the
        /// <br/> ResourceManager update and then on every update after it, is logged as an exception.
        /// </summary>
        private void CollectHandleErrors(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception ||
                condition.Contains("invalid operation handle", StringComparison.Ordinal) ||
                condition.Contains("reference count", StringComparison.Ordinal))
            {
                _handleErrors.Add(condition);
            }
        }

        private void AssertNoHandleErrors()
            => CollectionAssert.IsEmpty(_handleErrors,
                "No handle was read after its release or released twice, and nothing threw.");

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
        /// A second manager whose initialization the test supplies, torn down with the fixture.
        /// </summary>
        private AddressablesManager CreateManager(Func<AsyncOperationHandle<IResourceLocator>> initializeOperation)
        {
            AddressablesManager manager = new() { InitializeOperationOverride = initializeOperation };
            _extraManagers.Add(manager);
            return manager;
        }

        /// <summary>
        /// Addressables returns a chain operation when it has to wait for something before the catalog
        /// <br/> operation can start. Its debug name is the only public trace of that, and the tests read it
        /// <br/> to make sure they ran the path they describe.
        /// </summary>
        private static bool IsChainOperation(AsyncOperationHandle handle)
            => handle.IsValid() && handle.DebugName.StartsWith("ChainOperation", StringComparison.Ordinal);

        /// <summary>
        /// An initialization the test finishes itself, standing in for the Addressables one while it is
        /// <br/> still running. Like that one, it releases itself once it completes.
        /// </summary>
        private sealed class PendingInitialization : AsyncOperationBase<IResourceLocator>
        {
            public AsyncOperationHandle<IResourceLocator> OperationHandle { get; private set; }

            public static PendingInitialization Start()
            {
                PendingInitialization initialization = new();
                initialization.OperationHandle = Addressables.ResourceManager.StartOperation(initialization, default);
                initialization.OperationHandle.ReleaseHandleOnCompletion();
                return initialization;
            }

            protected override void Execute()
            {
            }

            public void Finish(bool isSuccess)
                => Complete(null, isSuccess, isSuccess ? null : FailedInitializationMessage);
        }

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
            AssertNoHandleErrors();

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
            AssertNoHandleErrors();
        }

        /// <summary>
        /// Addressables releases the check handle it still holds when a new check starts, so a second
        /// <br/> check that reached it while the first one ran would release the first handle twice.
        /// </summary>
        [UnityTest]
        public IEnumerator CheckForCatalogUpdates_Overlapping_BothCallbacksReceiveResults([Values(true, false)] bool autoReleaseHandle)
        {
            yield return InitializeManager();

            List<string> completed = new();
            IList<string> firstCatalogs = null;
            IList<string> secondCatalogs = null;
            _manager.CheckForCatalogUpdates(result =>
            {
                firstCatalogs = result;
                completed.Add("first");
            }, autoReleaseHandle);
            AsyncOperationHandle firstHandle = _manager.LastCatalogHandle;

            _manager.CheckForCatalogUpdates(result =>
            {
                secondCatalogs = result;
                completed.Add("second");
            }, autoReleaseHandle);
            Assert.IsTrue(_manager.LastCatalogHandle.Equals(firstHandle), "The second check waits while the first one runs.");

            yield return RunUntil(() => completed.Count == 2, "both checks completed");
            AsyncOperationHandle secondHandle = _manager.LastCatalogHandle;
            yield return RunFrames(SettleFrames);

            CollectionAssert.AreEqual(new[] { "first", "second" }, completed, "The checks completed in call order.");
            Assert.IsFalse(secondHandle.Equals(firstHandle), "The second check reached Addressables with a check of its own.");
            Assert.IsNotNull(firstCatalogs, "The first check received its result.");
            Assert.IsNotNull(secondCatalogs, "The second check received its result.");
            Assert.IsFalse(firstHandle.IsValid(), "The first check handle was released once the check finished.");
            Assert.IsFalse(secondHandle.IsValid(), "The second check handle was released once the check finished.");
            AssertNoHandleErrors();
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
            AssertNoHandleErrors();
        }

        /// <summary>
        /// An update that knows of no catalog update starts a check of its own, so one started inside a
        /// <br/> check callback, while that check handle is still held, would release it a second time.
        /// </summary>
        [UnityTest]
        public IEnumerator UpdateCatalogs_FromCheckCallback_ReachesAddressablesAfterCheckSettled([Values(true, false)] bool autoReleaseHandle)
        {
            yield return InitializeManager();

            // With nothing to update, Addressables fails the update with an error of its own.
            LogAssert.ignoreFailingMessages = true;

            AsyncOperationHandle checkHandle = default;
            IList<string> catalogs = null;
            bool isUpdateDeferred = false;
            bool isUpdated = false;
            _manager.CheckForCatalogUpdates(result =>
            {
                catalogs = result;
                _manager.UpdateCatalogs(_ => isUpdated = true, autoReleaseHandle);
                isUpdateDeferred = _manager.LastCatalogHandle.Equals(checkHandle);
            }, autoReleaseHandle);
            checkHandle = _manager.LastCatalogHandle;

            yield return RunUntil(() => !_manager.LastCatalogHandle.Equals(checkHandle), "UpdateCatalogs reached Addressables");
            Assert.IsFalse(checkHandle.IsValid(), "The check handle was settled before the update reached Addressables.");
            AsyncOperationHandle updateHandle = _manager.LastCatalogHandle;

            yield return RunUntil(() => isUpdated, "UpdateCatalogs completed");
            yield return RunFrames(SettleFrames);

            Assert.IsNotNull(catalogs, "The check received its result.");
            Assert.IsTrue(isUpdateDeferred, "The update did not reach Addressables inside the check callback.");
            Assert.IsFalse(updateHandle.IsValid(), "The update handle was released once the update finished.");
            AssertNoHandleErrors();
        }

        [UnityTest]
        public IEnumerator UpdateCatalogs_ManagerNotInitialized_InitializesManagerFirst([Values(true, false)] bool autoReleaseHandle)
        {
            // With nothing to update, Addressables fails the update with an error of its own.
            LogAssert.ignoreFailingMessages = true;

            List<string> steps = new();
            _manager.OnInitialized += () => steps.Add("initialized");
            _manager.OnInitializeResult += (isSuccess, error) => steps.Add($"initialize result {isSuccess}");
            _manager.UpdateCatalogs(_ => steps.Add("updated"), autoReleaseHandle);

            yield return RunUntil(() => steps.Contains("updated"), "UpdateCatalogs completed");
            yield return RunFrames(SettleFrames);

            CollectionAssert.AreEqual(new[] { "initialized", "initialize result True", "updated" }, steps,
                "The update ran the manager's initialization, with its events, before it went on.");
            Assert.IsTrue(_manager.IsInitialized, "The update left the manager initialized.");
            AssertNoHandleErrors();
        }

        [UnityTest]
        public IEnumerator UpdateCatalogs_WhileInitializing_ReachesAddressablesAfterInitialization()
        {
            // Addressables itself is initialized, so only the second manager's own initialization is pending.
            yield return InitializeManager();

            // With nothing to update, Addressables fails the update with an error of its own.
            LogAssert.ignoreFailingMessages = true;

            PendingInitialization initialization = PendingInitialization.Start();
            AddressablesManager manager = CreateManager(() => initialization.OperationHandle);
            bool hadReachedAddressablesAtResult = true;
            manager.OnInitializeResult += (isSuccess, error) => hadReachedAddressablesAtResult = manager.LastCatalogHandle.IsValid();
            bool isUpdated = false;
            manager.UpdateCatalogs(_ => isUpdated = true);

            Assert.IsTrue(manager.IsInitializing, "The update started the manager's initialization.");
            yield return RunFrames(SettleFrames);
            Assert.IsFalse(manager.LastCatalogHandle.IsValid(), "The update waits while the initialization runs.");

            initialization.Finish(isSuccess: true);
            Assert.IsTrue(manager.IsInitialized, "The initialization finished.");
            Assert.IsFalse(hadReachedAddressablesAtResult, "The initialization events ran before the update reached Addressables.");
            AsyncOperationHandle updateHandle = manager.LastCatalogHandle;
            Assert.IsTrue(updateHandle.IsValid(), "The update reached Addressables once the initialization finished.");

            yield return RunUntil(() => isUpdated, "UpdateCatalogs completed");
            yield return RunFrames(SettleFrames);

            Assert.IsFalse(updateHandle.IsValid(), "The update handle was released once the update finished.");
            Assert.IsFalse(initialization.OperationHandle.IsValid(), "The initialization released itself once.");
            AssertNoHandleErrors();
        }

        [UnityTest]
        public IEnumerator UpdateCatalogs_InitializationFails_ReportsNullWithoutReachingAddressables()
        {
            yield return InitializeManager();

            // Addressables logs the failed operation, and the manager logs the failed initialization.
            LogAssert.ignoreFailingMessages = true;

            int initializeCount = 0;
            PendingInitialization initialization = PendingInitialization.Start();
            AddressablesManager manager = CreateManager(() =>
            {
                initializeCount++;
                return initialization.OperationHandle;
            });
            bool? initializeResult = null;
            manager.OnInitializeResult += (isSuccess, error) => initializeResult = isSuccess;

            bool isFirstUpdated = false;
            IList<IResourceLocator> firstLocators = new List<IResourceLocator>();
            manager.UpdateCatalogs(result =>
            {
                firstLocators = result;
                isFirstUpdated = true;
            });

            initialization.Finish(isSuccess: false);
            yield return RunUntil(() => isFirstUpdated, "the first UpdateCatalogs completed");

            Assert.AreEqual(false, initializeResult, "OnInitializeResult reported the failed initialization.");
            Assert.AreEqual(FailedInitializationMessage, manager.InitializeError, "InitializeError holds the failure.");
            Assert.IsNull(firstLocators, "An update whose initialization failed reports a failure.");

            bool isSecondUpdated = false;
            IList<IResourceLocator> secondLocators = new List<IResourceLocator>();
            manager.UpdateCatalogs(result =>
            {
                secondLocators = result;
                isSecondUpdated = true;
            });

            Assert.IsTrue(isSecondUpdated, "An update after a failed initialization fails at once.");
            Assert.IsNull(secondLocators, "An update after a failed initialization reports a failure.");
            Assert.AreEqual(1, initializeCount, "The second update did not start another initialization.");
            Assert.IsFalse(manager.LastCatalogHandle.IsValid(), "Neither update reached Addressables.");

            yield return RunFrames(SettleFrames);
            AssertNoHandleErrors();
        }

        /// <summary>
        /// Addressables releases its initialization once it completes, so a manager that released it as
        /// <br/> well would destroy it inside its own completion, before the callbacks after its own ran.
        /// </summary>
        [UnityTest]
        public IEnumerator Dispose_WhileInitializing_LeavesTheInitializationToAddressables()
        {
            yield return InitializeManager();

            PendingInitialization initialization = PendingInitialization.Start();
            AddressablesManager manager = CreateManager(() => initialization.OperationHandle);
            manager.Initialize();
            Assert.IsTrue(manager.IsInitializing, "The initialization is running.");

            manager.Dispose();

            // Another user of the same initialization reads it once it completes.
            AsyncOperationStatus statusSeenByOtherCallback = AsyncOperationStatus.None;
            initialization.OperationHandle.Completed += operation => statusSeenByOtherCallback = operation.Status;

            initialization.Finish(isSuccess: true);
            yield return RunFrames(SettleFrames);

            Assert.AreEqual(AsyncOperationStatus.Succeeded, statusSeenByOtherCallback,
                "A callback after the manager's read the initialization while it was still valid.");
            Assert.IsFalse(manager.IsInitialized, "The completion did not reach the disposed manager.");
            Assert.IsFalse(initialization.OperationHandle.IsValid(), "The initialization released itself once.");
            AssertNoHandleErrors();
        }

        [UnityTest]
        public IEnumerator Dispose_CatalogCallsNotInAddressables_ReportNull()
        {
            yield return InitializeManager();

            PendingInitialization initialization = PendingInitialization.Start();
            AddressablesManager manager = CreateManager(() => initialization.OperationHandle);
            bool isUpdated = false;
            IList<IResourceLocator> locators = new List<IResourceLocator>();
            manager.UpdateCatalogs(result =>
            {
                locators = result;
                isUpdated = true;
            });

            bool isChecked = false;
            IList<string> catalogs = new List<string>();
            manager.CheckForCatalogUpdates(result =>
            {
                catalogs = result;
                isChecked = true;
            });

            manager.Dispose();

            Assert.IsTrue(isUpdated, "Dispose reported the update waiting for the initialization.");
            Assert.IsNull(locators, "The update waiting for the initialization reports a failure.");
            Assert.IsTrue(isChecked, "Dispose reported the check waiting for its turn.");
            Assert.IsNull(catalogs, "The check waiting for its turn reports a failure.");

            initialization.Finish(isSuccess: true);
            yield return RunFrames(SettleFrames);

            Assert.IsFalse(manager.LastCatalogHandle.IsValid(), "Neither call reached Addressables.");
            AssertNoHandleErrors();
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

            // The update waits for the check handle to be settled before it reaches Addressables.
            yield return RunUntil(() => !_manager.LastCatalogHandle.Equals(checkHandle), "UpdateCatalogsAsync reached Addressables");
            AsyncOperationHandle updateHandle = _manager.LastCatalogHandle;
            yield return RunUntil(() => update.Status != UniTaskStatus.Pending, "UpdateCatalogsAsync completed");
            update.GetAwaiter().GetResult();

            // A second release or a late read would surface while Addressables settles its callbacks.
            yield return RunFrames(SettleFrames);

            Assert.IsFalse(checkHandle.IsValid(), "The check handle was released once the check finished.");
            Assert.IsFalse(updateHandle.IsValid(), "The update handle was released once the update finished.");
            AssertNoHandleErrors();
        }
    }
}
#endif
