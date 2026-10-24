using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Mu3Library.DI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

#if MU3LIBRARY_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace Mu3Library.Tests
{
    /// <summary>
    /// A core's preparation in play mode, where the player loop runs it and Unity calls
    /// <c>OnEnable</c>, <c>OnDisable</c>, and <c>OnDestroy</c> as a scene does. Without UniTask
    /// the preparation is a coroutine, so these tests reach that path only in a project that
    /// does not install UniTask.
    /// </summary>
    public class CorePreparationTests
    {
        // Prepares until the test releases it. Each step records the preparation it belongs to, so
        // a test sees which preparations are still advancing; the coroutine version takes its
        // steps in a nested routine.
        private sealed class GatedCore : CoreBase
        {
            public bool IsReleased;
            public bool DisablesItselfOnSuccess;
            public int StartCount;
            public int PreparedCount;
            // The preparation each step belongs to, numbered from 1 in the order they started.
            public readonly List<int> Steps = new();

            protected override void Awake()
            {
                base.Awake();
                OnPrepared += () => PreparedCount++;
            }

#if MU3LIBRARY_UNITASK_SUPPORT
            protected override async UniTask<bool> PrepareCoreAsync(CancellationToken token)
            {
                int preparation = ++StartCount;
                while (!IsReleased)
                {
                    Steps.Add(preparation);
                    await UniTask.NextFrame(token);
                }

                return true;
            }
#else
            protected override IEnumerator PrepareCoreAsync(Action onSucceed)
            {
                int preparation = ++StartCount;
                yield return WaitForRelease(preparation);

                onSucceed();
                if (DisablesItselfOnSuccess)
                {
                    DisablesItselfOnSuccess = false;
                    enabled = false;
                }
            }

            private IEnumerator WaitForRelease(int preparation)
            {
                while (!IsReleased)
                {
                    Steps.Add(preparation);
                    yield return null;
                }
            }
#endif
        }

#if !MU3LIBRARY_UNITASK_SUPPORT
        // Prepares with the routine a test hands it before Start runs.
        private sealed class ScriptedCore : CoreBase
        {
            public Func<Action, IEnumerator> Routine;
            public int StartCount;
            public int PreparedCount;

            protected override void Awake()
            {
                base.Awake();
                OnPrepared += () => PreparedCount++;
            }

            protected override IEnumerator PrepareCoreAsync(Action onSucceed)
            {
                StartCount++;
                return Routine(onSucceed);
            }
        }
#endif

        private static readonly Regex _interruptedWarning = new("Core prepare was interrupted");

        private readonly List<GameObject> _objects = new();



        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject go in _objects)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }

            _objects.Clear();
            yield return null;

            // The cores registered with a root that outlives the test scene. It goes after them,
            // since a core destroyed after it would create a new one.
            CoreRoot root = Object.FindAnyObjectByType<CoreRoot>();
            if (root != null)
            {
                Object.Destroy(root.gameObject);
                yield return null;
            }
        }

        private GatedCore CreateCore()
        {
            return CreateCore<GatedCore>();
        }

        private T CreateCore<T>() where T : CoreBase
        {
            GameObject go = new(typeof(T).Name);
            _objects.Add(go);
            return go.AddComponent<T>();
        }

        // Start, which begins the preparation, runs on the frame after the core is created.
        private static IEnumerator WaitUntil(Func<bool> condition, string what, int frames = 10)
        {
            for (int frame = 0; frame < frames && !condition(); frame++)
            {
                yield return null;
            }

            Assert.IsTrue(condition(), $"Timed out waiting until {what}.");
        }

        private static IEnumerator AssertNoStepsFor(GatedCore core, string reason)
        {
            int steps = core.Steps.Count;
            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            Assert.AreEqual(steps, core.Steps.Count, reason);
        }

        private static IEnumerator AssertStepsFor(GatedCore core, string reason)
        {
            int steps = core.Steps.Count;
            for (int frame = 0; frame < 3; frame++)
            {
                yield return null;
            }

            Assert.Greater(core.Steps.Count, steps, reason);
        }

        // Once a later preparation has stepped, an earlier one never steps again, so no two of
        // them ran at the same time.
        private static void AssertOnePreparationAtATime(GatedCore core)
        {
            for (int i = 1; i < core.Steps.Count; i++)
            {
                Assert.GreaterOrEqual(
                    core.Steps[i],
                    core.Steps[i - 1],
                    $"A stopped preparation stepped after a later one: {string.Join(", ", core.Steps)}");
            }
        }

#if !MU3LIBRARY_UNITASK_SUPPORT
        [UnityTest]
        public IEnumerator Prepare_ObjectDeactivatedWhilePreparing_StartsOverWhenActiveAgain()
        {
            GatedCore core = CreateCore();
            yield return WaitUntil(() => core.IsPreparing, "the core started preparing");

            core.gameObject.SetActive(false);

            Assert.IsFalse(core.IsPreparing, "Deactivating the object stopped the preparation.");
            yield return AssertNoStepsFor(core, "The stopped preparation did not step while the object was inactive.");

            LogAssert.Expect(LogType.Warning, _interruptedWarning);
            core.gameObject.SetActive(true);

            Assert.AreEqual(2, core.StartCount, "Activating the object started the preparation over.");
            yield return AssertStepsFor(core, "The new preparation stepped.");

            core.IsReleased = true;
            yield return WaitUntil(() => core.IsPrepared, "the core prepared");

            Assert.AreEqual(1, core.PreparedCount);
            AssertOnePreparationAtATime(core);
        }

        [UnityTest]
        public IEnumerator Prepare_ComponentDisabledWhilePreparing_StartsOverWhenEnabledAgain()
        {
            GatedCore core = CreateCore();
            yield return WaitUntil(() => core.IsPreparing, "the core started preparing");

            core.enabled = false;

            Assert.IsFalse(core.IsPreparing, "Disabling the component stopped the preparation.");
            yield return AssertNoStepsFor(core, "The stopped preparation did not step while the component was disabled.");

            LogAssert.Expect(LogType.Warning, _interruptedWarning);
            core.enabled = true;

            Assert.AreEqual(2, core.StartCount, "Enabling the component started the preparation over.");
            yield return AssertStepsFor(core, "The new preparation stepped.");

            core.IsReleased = true;
            yield return WaitUntil(() => core.IsPrepared, "the core prepared");

            Assert.AreEqual(1, core.PreparedCount);
            AssertOnePreparationAtATime(core);
        }

        [UnityTest]
        public IEnumerator Prepare_ComponentDisabledThenObjectDeactivated_StartsOverWhenBothAreBack()
        {
            GatedCore core = CreateCore();
            yield return WaitUntil(() => core.IsPreparing, "the core started preparing");

            // Unity calls OnDisable only for the first of the two, and stops the coroutine
            // only at the second.
            core.enabled = false;
            core.gameObject.SetActive(false);
            yield return null;

            core.gameObject.SetActive(true);
            yield return AssertNoStepsFor(core, "The core did not prepare while its component stayed disabled.");

            Assert.AreEqual(1, core.StartCount);
            Assert.IsFalse(core.IsPreparing, "The core reports no preparation while none runs.");

            LogAssert.Expect(LogType.Warning, _interruptedWarning);
            core.enabled = true;

            Assert.AreEqual(2, core.StartCount, "Enabling the component started the preparation over.");
            yield return AssertStepsFor(core, "The new preparation stepped.");

            core.IsReleased = true;
            yield return WaitUntil(() => core.IsPrepared, "the core prepared");

            Assert.AreEqual(1, core.PreparedCount);
            AssertOnePreparationAtATime(core);
        }

        [UnityTest]
        public IEnumerator Prepare_RoutineDisablesItsCoreAsItSucceeds_StartsOverWhenEnabledAgain()
        {
            GatedCore core = CreateCore();
            core.DisablesItselfOnSuccess = true;
            yield return WaitUntil(() => core.IsPreparing, "the core started preparing");

            core.IsReleased = true;
            yield return WaitUntil(() => !core.enabled, "the preparation disabled its core");

            Assert.IsFalse(core.IsPrepared, "The preparation that the core's disabling stopped did not finish.");
            Assert.IsFalse(core.IsPreparing);
            Assert.AreEqual(0, core.PreparedCount);

            LogAssert.Expect(LogType.Warning, _interruptedWarning);
            core.enabled = true;
            yield return WaitUntil(() => core.IsPrepared, "the core prepared");

            Assert.AreEqual(2, core.StartCount);
            Assert.AreEqual(1, core.PreparedCount);
        }

        [UnityTest]
        public IEnumerator Prepare_DestroyedWhilePreparing_StopsWithoutPreparing()
        {
            GatedCore core = CreateCore();
            yield return WaitUntil(() => core.IsPreparing, "the core started preparing");

            Object.Destroy(core.gameObject);
            yield return null;

            Assert.IsFalse(CoreRoot.Instance.HasCore<GatedCore>(), "The destroyed core left the root.");
            yield return AssertNoStepsFor(core, "The destroyed core's preparation did not step.");

            core.IsReleased = true;
            yield return null;
            yield return null;

            Assert.AreEqual(1, core.StartCount);
            Assert.AreEqual(0, core.PreparedCount);
            // No warning announces a restart that a destroyed core never makes.
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Prepare_RoutineYieldsUnityInstructions_WaitsForEachAndPrepares()
        {
            bool isOpen = false;
            List<string> reached = new();
            ScriptedCore core = CreateCore<ScriptedCore>();
            core.Routine = onSucceed => WaitOnInstructions(onSucceed, () => isOpen, reached);

            yield return WaitUntil(() => reached.Contains("gate"), "the routine reached its WaitUntil", frames: 1000);
            yield return null;
            yield return null;

            Assert.IsTrue(core.IsPreparing);
            Assert.IsFalse(core.IsPrepared, "WaitUntil holds the preparation while its condition is false.");

            isOpen = true;
            yield return WaitUntil(() => core.IsPrepared, "the core prepared");

            CollectionAssert.AreEqual(new[] { "null", "WaitForSeconds", "WaitForFixedUpdate", "nested", "gate", "done" }, reached);
            Assert.AreEqual(1, core.PreparedCount);
        }

        [UnityTest]
        public IEnumerator Prepare_NestedRoutineThrows_LogsTheExceptionAndStaysUnprepared()
        {
            ScriptedCore core = CreateCore<ScriptedCore>();
            core.Routine = ThrowFromNestedRoutine;
            LogAssert.Expect(LogType.Exception, new Regex("prepare boom"));

            yield return WaitUntil(() => core.StartCount == 1 && !core.IsPreparing, "the preparation ended");
            yield return null;

            Assert.IsFalse(core.IsPrepared);
            Assert.AreEqual(0, core.PreparedCount);
            Assert.AreEqual(1, core.StartCount, "A failed preparation does not start over by itself.");
        }

        [UnityTest]
        public IEnumerator Prepare_RoutineIsNull_LogsAndStaysUnprepared()
        {
            ScriptedCore core = CreateCore<ScriptedCore>();
            core.Routine = _ => null;
            LogAssert.Expect(LogType.Exception, new Regex("ArgumentNullException"));

            yield return WaitUntil(() => core.StartCount == 1 && !core.IsPreparing, "the preparation ended");

            Assert.IsFalse(core.IsPrepared);
            Assert.AreEqual(0, core.PreparedCount);
        }

        private static IEnumerator WaitOnInstructions(Action onSucceed, Func<bool> isOpen, List<string> reached)
        {
            yield return null;
            reached.Add("null");

            yield return new WaitForSeconds(0.01f);
            reached.Add("WaitForSeconds");

            yield return new WaitForFixedUpdate();
            reached.Add("WaitForFixedUpdate");

            yield return NestedStep(reached);

            reached.Add("gate");
            yield return new WaitUntil(isOpen);
            reached.Add("done");

            onSucceed();
        }

        private static IEnumerator NestedStep(List<string> reached)
        {
            yield return null;
            reached.Add("nested");
        }

        private static IEnumerator ThrowFromNestedRoutine(Action onSucceed)
        {
            yield return Throw();
            onSucceed();
        }

        private static IEnumerator Throw()
        {
            yield return null;
            throw new InvalidOperationException("prepare boom");
        }
#else
        [UnityTest]
        public IEnumerator Prepare_ComponentDisabledThenObjectDeactivated_KeepsPreparing()
        {
            GatedCore core = CreateCore();
            yield return WaitUntil(() => core.IsPreparing, "the core started preparing");

            // The UniTask preparation runs on the player loop, apart from the core's object.
            core.enabled = false;
            core.gameObject.SetActive(false);

            Assert.IsTrue(core.IsPreparing);
            yield return AssertStepsFor(core, "The preparation went on while the object was inactive.");

            core.IsReleased = true;
            yield return WaitUntil(() => core.IsPrepared, "the core prepared");

            core.gameObject.SetActive(true);
            core.enabled = true;
            yield return null;

            Assert.AreEqual(1, core.StartCount, "Enabling the prepared core did not start another preparation.");
            Assert.AreEqual(1, core.PreparedCount);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Prepare_DestroyedWhilePreparing_CancelsWithoutPreparing()
        {
            GatedCore core = CreateCore();
            yield return WaitUntil(() => core.IsPreparing, "the core started preparing");

            LogAssert.Expect(LogType.Warning, new Regex("Core prepare canceled"));
            Object.Destroy(core.gameObject);
            yield return null;
            yield return null;

            Assert.IsFalse(CoreRoot.Instance.HasCore<GatedCore>(), "The destroyed core left the root.");
            Assert.IsFalse(core.IsPreparing);
            Assert.AreEqual(1, core.StartCount);
            Assert.AreEqual(0, core.PreparedCount);
        }
#endif
    }
}
