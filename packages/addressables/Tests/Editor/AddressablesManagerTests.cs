#if MU3LIBRARY_UNITASK_SUPPORT
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Mu3Library.Addressable;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.TestTools;

namespace Mu3Library.Tests
{
    /// <summary>
    /// Catalog checks and updates against the project's own catalog, which has no remote catalog and
    /// <br/> so nothing to update. That path finishes without a download, so it shows that the calls
    /// <br/> complete and handle each Addressables handle once, not that a real update works. List loads
    /// <br/> run against a label served from memory, since the project's Addressable groups hold no entry.
    /// </summary>
    public class AddressablesManagerTests
    {
        private const double MaxSeconds = 20.0;
        private const int SettleFrames = 5;
        private const string FailedInitializationMessage = "Test initialization failed.";
        private const string ThrowingInitializationMessage = "Test initialization threw.";
        private const string InMemoryLabelKey = "in-memory-label";
        private static readonly string[] InMemoryLabelAssets = { "first asset", "second asset" };
        private static readonly string[] CallsForInMemoryLabel = { "first asset", "second asset", "list: first asset, second asset" };

        private AddressablesManager _manager;
        private readonly List<AddressablesManager> _extraManagers = new();
        private readonly List<InMemoryLabel> _inMemoryLabels = new();
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

            // A load a failed test left waiting finishes before the managers release it.
            foreach (InMemoryLabel label in _inMemoryLabels)
            {
                label.FinishHeldLoads();
            }

            _manager.Dispose();
            foreach (AddressablesManager manager in _extraManagers)
            {
                manager.Dispose();
            }

            foreach (InMemoryLabel label in _inMemoryLabels)
            {
                label.Remove();
            }

            _extraManagers.Clear();
            _inMemoryLabels.Clear();
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
        /// A label whose assets are strings served from memory, since the project's Addressable groups
        /// <br/> hold no entry. While <see cref="IsHoldingLoads"/> is set, a load waits until
        /// <br/> <see cref="FinishHeldLoads"/>, so a test can join it while it is still in progress.
        /// </summary>
        private sealed class InMemoryLabel : ResourceProviderBase
        {
            private readonly ResourceLocationMap _locator;
            private readonly List<ProvideHandle> _heldLoads = new();

            public bool IsHoldingLoads { get; set; }

            public InMemoryLabel(string key, IReadOnlyList<string> assets)
            {
                // The ResourceManager keeps the provider it found for an id even once the provider is
                // removed, so each label takes an id of its own.
                m_ProviderId = $"{typeof(InMemoryLabel).FullName}.{Guid.NewGuid():N}";
                _locator = new ResourceLocationMap(m_ProviderId);
                foreach (string asset in assets)
                {
                    _locator.Add(key, new ResourceLocationBase(asset, asset, m_ProviderId, typeof(string)));
                }

                Addressables.ResourceManager.ResourceProviders.Add(this);
                Addressables.AddResourceLocator(_locator);
            }

            public override void Provide(ProvideHandle provideHandle)
            {
                if (IsHoldingLoads)
                {
                    _heldLoads.Add(provideHandle);
                    return;
                }

                provideHandle.Complete(provideHandle.Location.InternalId, true, null);
            }

            public void FinishHeldLoads()
            {
                IsHoldingLoads = false;
                foreach (ProvideHandle load in _heldLoads)
                {
                    load.Complete(load.Location.InternalId, true, null);
                }

                _heldLoads.Clear();
            }

            public void Remove()
            {
                Addressables.RemoveResourceLocator(_locator);
                Addressables.ResourceManager.ResourceProviders.Remove(this);
            }
        }

        /// <summary>
        /// A label of <see cref="InMemoryLabelAssets"/> under <see cref="InMemoryLabelKey"/>, removed with
        /// <br/> the fixture. Addressables must be initialized first.
        /// </summary>
        private InMemoryLabel CreateInMemoryLabel()
        {
            InMemoryLabel label = new(InMemoryLabelKey, InMemoryLabelAssets);
            _inMemoryLabels.Add(label);
            return label;
        }

        private static string DescribeList(IList<string> assets)
            => assets == null ? "no list" : $"list: {string.Join(", ", assets)}";

        private async UniTask LoadAssetsAsyncAndRecord(List<string> calls)
        {
            IList<string> assets = await _manager.LoadAssetsAsync<string>(InMemoryLabelKey, calls.Add);
            calls.Add(DescribeList(assets));
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

        [Test]
        public void Initialize_InitializeAsyncThrows_LeavesNoInitializationRunning()
        {
            AddressablesManager manager = CreateManager(() => throw new InvalidOperationException(ThrowingInitializationMessage));

            Assert.Throws<InvalidOperationException>(() => manager.Initialize());
            Assert.IsFalse(manager.IsInitializing, "An initialization that threw is not left running.");

            // A catalog update logs the throw and reports it as a failure instead of waiting for it.
            LogAssert.Expect(LogType.Exception, new Regex(Regex.Escape(ThrowingInitializationMessage)));
            bool isUpdated = false;
            IList<IResourceLocator> locators = new List<IResourceLocator>();
            manager.UpdateCatalogs(result =>
            {
                locators = result;
                isUpdated = true;
            });

            Assert.IsTrue(isUpdated, "The update reported the throw at once.");
            Assert.IsNull(locators, "The update reports a failure.");
            Assert.IsFalse(manager.IsInitializing, "The update left no initialization running.");
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

        /// <summary>
        /// A label loaded again, by another feature or a later screen, is served by the first load: from
        /// <br/> the cache once it finished, or by joining it while it runs. The second call still gets
        /// <br/> each asset through its per-asset callback before the list, as the first call does.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadAssets_SameKeyAgain_HandsEachAssetBeforeTheList([Values(false, true)] bool isFirstLoadInProgress)
        {
            yield return InitializeManager();
            InMemoryLabel label = CreateInMemoryLabel();
            label.IsHoldingLoads = isFirstLoadInProgress;

            List<string> firstCalls = new();
            bool isFirstListReceived = false;
            _manager.LoadAssets<string>(InMemoryLabelKey, firstCalls.Add, assets =>
            {
                firstCalls.Add(DescribeList(assets));
                isFirstListReceived = true;
            });
            if (!isFirstLoadInProgress)
            {
                yield return RunUntil(() => isFirstListReceived, "the first LoadAssets completed");
            }

            List<string> secondCalls = new();
            bool isSecondListReceived = false;
            _manager.LoadAssets<string>(InMemoryLabelKey, secondCalls.Add, assets =>
            {
                secondCalls.Add(DescribeList(assets));
                isSecondListReceived = true;
            });
            if (isFirstLoadInProgress)
            {
                CollectionAssert.IsEmpty(secondCalls, "A call that joins a load in progress waits for it.");
                label.FinishHeldLoads();
            }

            yield return RunUntil(() => isFirstListReceived && isSecondListReceived, "both LoadAssets calls completed");
            yield return RunFrames(SettleFrames);

            CollectionAssert.AreEqual(CallsForInMemoryLabel, secondCalls, "The second call got each asset, then the list.");
            CollectionAssert.AreEquivalent(CallsForInMemoryLabel, firstCalls, "The first call got each asset and the list.");
            AssertNoHandleErrors();
        }

        [UnityTest]
        public IEnumerator LoadAssetsAsync_SameKeyAgain_HandsEachAssetBeforeReturning([Values(false, true)] bool isFirstLoadInProgress)
        {
            yield return InitializeManager();
            InMemoryLabel label = CreateInMemoryLabel();
            label.IsHoldingLoads = isFirstLoadInProgress;

            List<string> firstCalls = new();
            UniTask firstLoad = LoadAssetsAsyncAndRecord(firstCalls);
            if (!isFirstLoadInProgress)
            {
                yield return RunUntil(() => firstLoad.Status != UniTaskStatus.Pending, "the first LoadAssetsAsync completed");
            }

            List<string> secondCalls = new();
            UniTask secondLoad = LoadAssetsAsyncAndRecord(secondCalls);
            if (isFirstLoadInProgress)
            {
                CollectionAssert.IsEmpty(secondCalls, "A call that joins a load in progress waits for it.");
                label.FinishHeldLoads();
            }

            yield return RunUntil(() => firstLoad.Status != UniTaskStatus.Pending && secondLoad.Status != UniTaskStatus.Pending,
                "both LoadAssetsAsync calls completed");
            firstLoad.GetAwaiter().GetResult();
            secondLoad.GetAwaiter().GetResult();
            yield return RunFrames(SettleFrames);

            CollectionAssert.AreEqual(CallsForInMemoryLabel, secondCalls, "The second call got each asset, then the list.");
            CollectionAssert.AreEquivalent(CallsForInMemoryLabel, firstCalls, "The first call got each asset and the list.");
            AssertNoHandleErrors();
        }
        private const string FirstInMemoryAsset = "first asset";

        private async UniTask<AssetHandle<string>> LoadHandle()
            => await _manager.LoadAssetHandleAsync<string>(InMemoryLabelKey);

        private IEnumerator Await<T>(UniTask<T> task, Action<T> result, string description)
        {
            yield return RunUntil(() => task.Status != UniTaskStatus.Pending, description);
            result(task.GetAwaiter().GetResult());
        }

        /// <summary>
        /// Two callers that load the same key and type with handles share one cached asset, which
        /// <br/> stays while either holds its handle and goes once both disposed theirs.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadAssetHandle_TwoCallers_KeepTheAssetUntilBothDispose()
        {
            yield return InitializeManager();
            CreateInMemoryLabel();

            AssetHandle<string> first = null;
            AssetHandle<string> second = null;
            yield return Await(LoadHandle(), handle => first = handle, "the first handle arrived");
            bool isSecondReceived = false;
            _manager.LoadAssetHandle<string>(InMemoryLabelKey, handle =>
            {
                second = handle;
                isSecondReceived = true;
            });
            yield return RunUntil(() => isSecondReceived, "the second handle arrived");

            Assert.That(first.IsValid && second.IsValid, Is.True);
            Assert.That(first.Asset, Is.EqualTo(FirstInMemoryAsset));
            Assert.That(_manager.GetHandleCount<string>(InMemoryLabelKey), Is.EqualTo(2));

            first.Dispose();
            first.Dispose();
            Assert.That(first.IsValid, Is.False);
            Assert.That(first.Asset, Is.Null);
            Assert.That(_manager.GetHandleCount<string>(InMemoryLabelKey), Is.EqualTo(1), "A second dispose counts nothing.");
            Assert.That(second.Asset, Is.EqualTo(FirstInMemoryAsset), "The other caller keeps the asset.");
            Assert.That(_manager.TryGetCachedAsset(InMemoryLabelKey, out string cached) && cached == FirstInMemoryAsset, Is.True);

            second.Dispose();
            Assert.That(_manager.GetHandleCount<string>(InMemoryLabelKey), Is.EqualTo(0));
            Assert.That(_manager.TryGetCachedAsset(InMemoryLabelKey, out string _), Is.False, "The last handle released the asset.");
            Assert.That(_manager.TrackedHandleCount, Is.EqualTo(0));
            yield return RunFrames(SettleFrames);
            AssertNoHandleErrors();
        }

        /// <summary>
        /// Callers that join a load still in progress each get a handle that counts once it completes.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadAssetHandle_CallersThatJoinALoadInProgress_EachCount()
        {
            yield return InitializeManager();
            InMemoryLabel label = CreateInMemoryLabel();
            label.IsHoldingLoads = true;

            var handles = new List<AssetHandle<string>>();
            _manager.LoadAssetHandle<string>(InMemoryLabelKey, handles.Add);
            _manager.LoadAssetHandle<string>(InMemoryLabelKey, handles.Add);
            UniTask<AssetHandle<string>> third = LoadHandle();
            yield return RunFrames(SettleFrames);
            CollectionAssert.IsEmpty(handles, "A call that joins a load in progress waits for it.");

            label.FinishHeldLoads();
            yield return Await(third, handles.Add, "the joined loads completed");
            yield return RunUntil(() => handles.Count == 3, "every handle arrived");

            Assert.That(handles.TrueForAll(handle => handle.IsValid && handle.Asset == FirstInMemoryAsset), Is.True);
            Assert.That(_manager.GetHandleCount<string>(InMemoryLabelKey), Is.EqualTo(3));
            foreach (AssetHandle<string> handle in handles)
            {
                handle.Dispose();
            }

            Assert.That(_manager.TryGetCachedAsset(InMemoryLabelKey, out string _), Is.False);
            yield return RunFrames(SettleFrames);
            AssertNoHandleErrors();
        }

        /// <summary>
        /// <see cref="AddressablesManager.ReleaseCachedAsset"/> still releases the key for every caller, so
        /// <br/> the handles left read not valid, and disposing them afterwards releases nothing again.
        /// </summary>
        [UnityTest]
        public IEnumerator ReleaseCachedAsset_LeavesTheRemainingHandlesInvalid()
        {
            yield return InitializeManager();
            CreateInMemoryLabel();

            AssetHandle<string> before = null;
            yield return Await(LoadHandle(), handle => before = handle, "the handle arrived");
            _manager.ReleaseCachedAsset(InMemoryLabelKey);

            Assert.That(before.IsValid, Is.False);
            Assert.That(before.Asset, Is.Null);
            Assert.That(_manager.GetHandleCount<string>(InMemoryLabelKey), Is.EqualTo(0));

            AssetHandle<string> after = null;
            yield return Await(LoadHandle(), handle => after = handle, "a handle of the reload arrived");
            before.Dispose();
            Assert.That(after.IsValid, Is.True, "Disposing a handle from before the release leaves the reload alone.");
            Assert.That(_manager.GetHandleCount<string>(InMemoryLabelKey), Is.EqualTo(1));

            after.Dispose();
            Assert.That(_manager.TryGetCachedAsset(InMemoryLabelKey, out string _), Is.False);
            yield return RunFrames(SettleFrames);
            AssertNoHandleErrors();
        }

        /// <summary>
        /// The load without a handle keeps its behavior: it counts nothing, and one release frees the
        /// <br/> key for every such caller.
        /// </summary>
        [UnityTest]
        public IEnumerator LoadAsset_WithoutAHandle_KeepsItsBehavior()
        {
            yield return InitializeManager();
            CreateInMemoryLabel();

            int loads = 0;
            _manager.LoadAsset<string>(InMemoryLabelKey, _ => loads++);
            yield return RunUntil(() => loads == 1, "the first LoadAsset completed");
            _manager.LoadAsset<string>(InMemoryLabelKey, _ => loads++);
            Assert.That(loads, Is.EqualTo(2), "The second load comes from the cache at once.");
            Assert.That(_manager.GetHandleCount<string>(InMemoryLabelKey), Is.EqualTo(0));

            _manager.ReleaseCachedAsset(InMemoryLabelKey);
            Assert.That(_manager.TryGetCachedAsset(InMemoryLabelKey, out string _), Is.False);
            yield return RunFrames(SettleFrames);
            AssertNoHandleErrors();
        }
    }
}
#endif
