using System.Collections;
using System.Collections.Generic;
using Mu3Library.DI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Mu3Library.Tests
{
    /// <summary>
    /// The <see cref="CoreRoot"/> accessor in play mode, where it creates the root it hands out
    /// and, once the application quits, stops doing so. A test cannot quit the application, so
    /// it marks the shutdown the way <c>Application.quitting</c> does.
    /// </summary>
    public class CoreRootLifetimeTests
    {
        private sealed class LifetimeCore : CoreBase
        {
        }

        private readonly List<GameObject> _objects = new();



        // Each test starts without a root, so what it asserts about the root is its own doing.
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            CoreRoot root = Object.FindAnyObjectByType<CoreRoot>();
            if (root != null)
            {
                Object.Destroy(root.gameObject);
                yield return null;
            }

            Assert.IsNull(Object.FindAnyObjectByType<CoreRoot>());
        }

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

            CoreRoot root = Object.FindAnyObjectByType<CoreRoot>();
            if (root != null)
            {
                Object.Destroy(root.gameObject);
                yield return null;
            }

            CoreRoot.ResetShutdownState();
        }

        private LifetimeCore CreateCore()
        {
            GameObject go = new(nameof(LifetimeCore));
            _objects.Add(go);
            return go.AddComponent<LifetimeCore>();
        }

        [UnityTest]
        public IEnumerator Instance_WithoutRoot_CreatesOneThatOutlivesTheScene()
        {
            ICoreRoot instance = CoreRoot.Instance;
            yield return null;

            Assert.IsNotNull(instance);
            Assert.AreSame(instance, CoreRoot.Instance);
            Assert.AreSame(instance, Object.FindAnyObjectByType<CoreRoot>());
            Assert.AreEqual("DontDestroyOnLoad", ((CoreRoot)instance).gameObject.scene.name);
        }

        [UnityTest]
        public IEnumerator Instance_WhileShuttingDown_ReturnsNullAndCreatesNoRoot()
        {
            CoreRoot.MarkShuttingDown();

            Assert.IsNull(CoreRoot.Instance);
            yield return null;

            Assert.IsNull(Object.FindAnyObjectByType<CoreRoot>());
        }

        [UnityTest]
        public IEnumerator Instance_WhileShuttingDown_StillReturnsTheLiveRoot()
        {
            CreateCore();
            ICoreRoot instance = CoreRoot.Instance;
            Assert.IsTrue(instance.HasCore<LifetimeCore>());

            CoreRoot.MarkShuttingDown();
            yield return null;

            Assert.AreSame(instance, CoreRoot.Instance, "Only a missing root is no longer created.");
            Assert.IsTrue(CoreRoot.Instance.HasCore<LifetimeCore>());
        }

        [UnityTest]
        public IEnumerator CoreDestroyedAfterTheRootWhileShuttingDown_LeavesNoRootBehind()
        {
            LifetimeCore core = CreateCore();
            CoreRoot root = Object.FindAnyObjectByType<CoreRoot>();
            Assert.IsNotNull(root);
            Assert.IsTrue(root.HasCore<LifetimeCore>());

            // Quitting destroys the root first, and the core then leaves a root that is gone.
            CoreRoot.MarkShuttingDown();
            Object.Destroy(root.gameObject);
            yield return null;

            Object.Destroy(core.gameObject);
            yield return null;

            Assert.IsNull(Object.FindAnyObjectByType<CoreRoot>(), "The destroyed core created no new root.");
        }
    }
}
