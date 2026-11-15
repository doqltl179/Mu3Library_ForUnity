#if MU3LIBRARY_ADDRESSABLES_SUPPORT
using System;
using System.Collections;
using System.Collections.Generic;
using Mu3Library.Scene;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

namespace Mu3Library.Tests
{
    /// <summary>
    /// Addressables scene loads for a key no catalog holds. They fail whether or not Addressables
    /// <br/> initializes in edit mode, which is the failure path the loader has to report.
    /// </summary>
    public class SceneLoaderAddressablesTests
    {
        private const double MaxSeconds = 20.0;
        private const string MissingKey = "Mu3Library.Tests/MissingAddressableScene";

        private SceneLoader _loader;
        private readonly List<SceneLoadFailedInfo> _failures = new();
        private readonly List<bool> _isLoadingWhenReported = new();

        [SetUp]
        public void SetUp()
        {
            _loader = new SceneLoader();
            _loader.OnSceneLoadFailed += info =>
            {
                _failures.Add(info);
                _isLoadingWhenReported.Add(_loader.IsLoading);
            };
        }

        [TearDown]
        public void TearDown()
        {
            _loader.Dispose();
            _failures.Clear();
            _isLoadingWhenReported.Clear();

            LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>
        /// Addressables reports the unknown key itself, with errors that depend on whether it was
        /// <br/> already initialized, and the loader logs the failed load. Called from each test body,
        /// <br/> since the test runner resets the flag when the test starts.
        /// </summary>
        private static void IgnoreLoadFailureLogs()
        {
            LogAssert.ignoreFailingMessages = true;
        }

        private IEnumerator RunUntil(Func<bool> condition, string description)
        {
            double start = EditorApplication.timeSinceStartup;
            while (!condition() && EditorApplication.timeSinceStartup - start < MaxSeconds)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                yield return null;

                _loader.Update();
            }

            Assert.IsTrue(condition(), $"Waited {MaxSeconds} seconds until {description}.");
        }

        [UnityTest]
        public IEnumerator LoadSingleSceneWithAddressables_UnknownKey_ReportsTheFailureAfterTheCommandEnded()
        {
            IgnoreLoadFailureLogs();
            string currentScene = _loader.CurrentSceneName;

            _loader.LoadSingleSceneWithAddressables(MissingKey);
            Assert.IsTrue(_loader.IsLoading, "The load started.");

            yield return RunUntil(() => _failures.Count > 0, "the failure was reported");

            Assert.AreEqual(1, _failures.Count);
            Assert.AreEqual(MissingKey, _failures[0].Target);
            Assert.IsFalse(_failures[0].IsAdditive);
            Assert.IsNotEmpty(_failures[0].ErrorMessage, "The failure carries the message Addressables gave.");
            Assert.IsFalse(_isLoadingWhenReported[0], "The failed command is over by the time it is reported.");
            Assert.IsFalse(_loader.IsLoading);
            Assert.AreEqual(0, _loader.LoadingCount);

            // The status reports the scene that was standing, not the key of the failed load. An
            // unnamed scene, such as the one a test run may start in, reports no status at all.
            Assert.AreEqual(currentScene, _loader.CurrentSceneName);
            bool hasStatus = _loader.TryGetSingleSceneStatus(out SceneStatus status);
            Assert.AreEqual(!string.IsNullOrEmpty(currentScene), hasStatus);
            if (hasStatus)
            {
                Assert.AreEqual(currentScene, status.SceneName);
                Assert.AreEqual(ScenePhase.Loaded, status.Phase);
            }
        }

        [UnityTest]
        public IEnumerator LoadAdditiveSceneWithAddressables_UnknownKey_ReportsTheFailureAfterTheCommandEnded()
        {
            IgnoreLoadFailureLogs();
            _loader.LoadAdditiveSceneWithAddressables(MissingKey);
            Assert.IsTrue(_loader.IsLoading, "The load started.");

            yield return RunUntil(() => _failures.Count > 0, "the failure was reported");

            Assert.AreEqual(1, _failures.Count);
            Assert.AreEqual(MissingKey, _failures[0].Target);
            Assert.IsTrue(_failures[0].IsAdditive);
            Assert.IsFalse(_isLoadingWhenReported[0], "The failed command is over by the time it is reported.");
            Assert.IsFalse(_loader.IsLoading);
            Assert.IsFalse(_loader.TryGetAdditiveSceneStatus(MissingKey, out _));
            Assert.IsFalse(_loader.IsSceneLoadedAsAdditive(MissingKey));
        }

        [UnityTest]
        public IEnumerator OnSceneLoadFailed_HandlerStartsTheNextLoad_IsNotRejectedAsBusy()
        {
            IgnoreLoadFailureLogs();
            List<SceneCommandRejectedInfo> rejections = new();
            _loader.OnSceneCommandRejected += rejections.Add;

            bool retried = false;
            _loader.OnSceneLoadFailed += _ =>
            {
                if (!retried)
                {
                    retried = true;
                    _loader.LoadSingleSceneWithAddressables(MissingKey);
                }
            };

            _loader.LoadSingleSceneWithAddressables(MissingKey);
            yield return RunUntil(() => _failures.Count > 1, "the retried load failed too");

            Assert.IsEmpty(rejections, "A handler may start the next load as soon as the failure is reported.");
            Assert.IsFalse(_loader.IsLoading);
        }
    }
}
#endif
