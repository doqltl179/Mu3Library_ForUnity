using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Mu3Library.DI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Mu3Library.Tests
{
    public class CoreRootTests
    {
        private sealed class CoreProbe : IInitializable, IUpdatable, ILateUpdatable
        {
            public Action Initialized;
            public Action Updated;
            public Action LateUpdated;

            public void Initialize() => Initialized?.Invoke();
            public void Update() => Updated?.Invoke();
            public void LateUpdate() => LateUpdated?.Invoke();
        }

        private abstract class ProbeCore : CoreBase
        {
            public CoreProbe Probe => GetClass<CoreProbe>();

            protected override void ConfigureContainer()
            {
                RegisterClass<CoreProbe>();
            }
        }

        // With equal execution orders the root sorts cores by type name.
        private sealed class CoreA : ProbeCore { }
        private sealed class CoreB : ProbeCore { }
        private sealed class CoreC : ProbeCore { }

        private readonly List<GameObject> _objects = new();
        private CoreRoot _root;



        [SetUp]
        public void SetUp()
        {
            _root = CreateObject(nameof(CoreRoot)).AddComponent<CoreRoot>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _objects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _objects.Clear();
        }

        private GameObject CreateObject(string name)
        {
            GameObject go = new(name);
            _objects.Add(go);
            return go;
        }

        // Awake never runs on a component added in edit mode, so the test builds the container
        // the way Awake does and registers the core with its own root.
        private T CreateCore<T>() where T : ProbeCore
        {
            T core = CreateObject(typeof(T).Name).AddComponent<T>();
            core.InitializeContainer();
            return core;
        }

        [Test]
        public void UpdateCores_CoreRegisteredDuringWalk_JoinsNextFrameWithoutReorder()
        {
            List<string> updates = new();
            List<string> lateUpdates = new();
            CoreA a = CreateCore<CoreA>();
            CoreB b = CreateCore<CoreB>();
            CoreC c = CreateCore<CoreC>();
            _root.RegisterCore(b);
            _root.RegisterCore(c);

            // CoreA sorts before the core that registers it.
            b.Probe.Updated = () =>
            {
                updates.Add("B");
                if (!_root.HasCore<CoreA>())
                {
                    _root.RegisterCore(a);
                }
            };
            c.Probe.Updated = () => updates.Add("C");
            a.Probe.Updated = () => updates.Add("A");
            a.Probe.LateUpdated = () => lateUpdates.Add("A");

            _root.UpdateCores();
            _root.LateUpdateCores();

            CollectionAssert.AreEqual(new[] { "B", "C" }, updates);
            CollectionAssert.IsEmpty(lateUpdates);
            Assert.IsTrue(a.IsInitialized);

            updates.Clear();
            _root.UpdateCores();
            _root.LateUpdateCores();

            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, updates);
            CollectionAssert.AreEqual(new[] { "A" }, lateUpdates);
        }

        [Test]
        public void UpdateCores_CoreUnregisteredDuringWalk_DoesNotSkipTheNext()
        {
            List<string> updates = new();
            CoreA a = CreateCore<CoreA>();
            CoreB b = CreateCore<CoreB>();
            CoreC c = CreateCore<CoreC>();
            _root.RegisterCore(a);
            _root.RegisterCore(b);
            _root.RegisterCore(c);

            a.Probe.Updated = () =>
            {
                updates.Add("A");
                _root.UnregisterCore(a);
            };
            b.Probe.Updated = () => updates.Add("B");
            c.Probe.Updated = () => updates.Add("C");

            _root.UpdateCores();

            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, updates);
            CollectionAssert.AreEqual(new CoreBase[] { b, c }, _root.RegisteredCores);
        }

        [Test]
        public void UpdateCores_ThrowingService_OtherCoresStillUpdate()
        {
            List<string> updates = new();
            CoreA a = CreateCore<CoreA>();
            CoreB b = CreateCore<CoreB>();
            _root.RegisterCore(a);
            _root.RegisterCore(b);

            a.Probe.Updated = () => throw new InvalidOperationException("core update boom");
            b.Probe.Updated = () => updates.Add("B");

            LogAssert.Expect(LogType.Exception, new Regex("core update boom"));
            _root.UpdateCores();

            CollectionAssert.AreEqual(new[] { "B" }, updates);
        }

        [Test]
        public void RegisterCore_ThrowingServiceInitialize_StillReportsInitializedAndPrepared()
        {
            List<Type> initialized = new();
            List<Type> prepared = new();
            _root.OnCoreInitialized += type => initialized.Add(type);
            _root.OnCorePrepared += type => prepared.Add(type);

            CoreA a = CreateCore<CoreA>();
            a.Probe.Initialized = () => throw new InvalidOperationException("initialize boom");

            LogAssert.Expect(LogType.Exception, new Regex("initialize boom"));
            _root.RegisterCore(a);

            Assert.IsTrue(a.IsInitialized);
            CollectionAssert.AreEqual(new[] { typeof(CoreA) }, initialized);
            AssertPreparedNotifies(a, prepared);
        }

        [Test]
        public void RegisterCore_ThrowingInitializedHandler_StillWiresPrepared()
        {
            List<Type> prepared = new();
            _root.OnCoreInitialized += _ => throw new InvalidOperationException("handler boom");
            _root.OnCorePrepared += type => prepared.Add(type);

            CoreA a = CreateCore<CoreA>();

            LogAssert.Expect(LogType.Exception, new Regex("handler boom"));
            _root.RegisterCore(a);

            Assert.IsTrue(a.IsInitialized);
            AssertPreparedNotifies(a, prepared);
        }

        private static void AssertPreparedNotifies(CoreBase core, List<Type> prepared)
        {
#if MU3LIBRARY_UNITASK_SUPPORT
            // The default UniTask preparation completes synchronously, so edit mode observes it.
            core.PrepareCore();

            Assert.IsTrue(core.IsPrepared);
            CollectionAssert.AreEqual(new[] { core.GetType() }, prepared);
#else
            // The coroutine path needs the player loop; preparation is checked in play mode.
            Assert.Pass("Coroutine preparation is not observable in edit mode.");
#endif
        }
    }

    public class CoreRootShutdownTests
    {
        private sealed class ShutdownCore : CoreBase
        {
            public void InvokeOnDestroy() => OnDestroy();
        }

        private readonly List<GameObject> _objects = new();



        [TearDown]
        public void TearDown()
        {
            CoreRoot.ResetShutdownState();

            foreach (GameObject go in _objects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void Instance_WhileShuttingDown_ReturnsNullAndCreatesNoRoot()
        {
            Assume.That(Object.FindAnyObjectByType<CoreRoot>(), Is.Null);

            CoreRoot.MarkShuttingDown();

            Assert.IsNull(CoreRoot.InstanceInternal);
            Assert.IsNull(CoreRoot.Instance);
            Assert.IsNull(Object.FindAnyObjectByType<CoreRoot>());
        }

        [Test]
        public void OnDestroy_WhileShuttingDown_PassesTheMissingRoot()
        {
            Assume.That(Object.FindAnyObjectByType<CoreRoot>(), Is.Null);

            GameObject go = new(nameof(ShutdownCore));
            _objects.Add(go);
            ShutdownCore core = go.AddComponent<ShutdownCore>();
            core.InitializeContainer();

            CoreRoot.MarkShuttingDown();

            Assert.DoesNotThrow(core.InvokeOnDestroy);
            Assert.IsNull(Object.FindAnyObjectByType<CoreRoot>());
        }
    }
}
