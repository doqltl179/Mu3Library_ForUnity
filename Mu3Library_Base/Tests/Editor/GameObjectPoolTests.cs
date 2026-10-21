using System.Collections.Generic;
using Mu3Library.ObjectPool;
using NUnit.Framework;
using UnityEngine;

namespace Mu3Library.Tests
{
    public class GameObjectPoolTests
    {
        private readonly List<GameObject> _createdObjects = new();



        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _createdObjects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _createdObjects.Clear();
        }

        private Transform CreateObject()
        {
            GameObject go = new("PooledObject");
            _createdObjects.Add(go);
            return go.transform;
        }

        [Test]
        public void Dequeue_EmptyPoolWithCreate_CreatesObject()
        {
            GameObjectPool<Transform> pool = new(CreateObject);

            Transform obj = pool.Dequeue();

            Assert.IsNotNull(obj);
        }

        [Test]
        public void Enqueue_Dequeue_ReturnsPooledObject()
        {
            GameObjectPool<Transform> pool = new(CreateObject);
            Transform obj = CreateObject();

            pool.Enqueue(obj);
            Transform dequeued = pool.Dequeue();

            Assert.AreSame(obj, dequeued);
        }

        [Test]
        public void Enqueue_SameObjectTwice_KeepsOneEntry()
        {
            GameObjectPool<Transform> pool = new();
            Transform obj = CreateObject();

            pool.Enqueue(obj);
            pool.Enqueue(obj);

            Assert.AreEqual(1, pool.Count);
        }

        // A component may define equality by value; the pool still has to tell two of them apart.
        private sealed class ValueEqualComponent : MonoBehaviour
        {
            public override bool Equals(object other) => other is ValueEqualComponent;

            public override int GetHashCode() => 0;
        }

        [Test]
        public void Enqueue_ComponentsEqualByOverride_KeepsBoth()
        {
            GameObjectPool<ValueEqualComponent> pool = new();
            ValueEqualComponent first = CreateObject().gameObject.AddComponent<ValueEqualComponent>();
            ValueEqualComponent second = CreateObject().gameObject.AddComponent<ValueEqualComponent>();

            pool.Enqueue(first);
            pool.Enqueue(second);

            Assert.AreEqual(2, pool.Count);
        }

        [Test]
        public void Enqueue_AfterDequeue_PoolsObjectAgain()
        {
            GameObjectPool<Transform> pool = new();
            Transform obj = CreateObject();
            pool.Enqueue(obj);
            pool.Dequeue();

            pool.Enqueue(obj);

            Assert.AreEqual(1, pool.Count);
        }

        [Test]
        public void Dequeue_ObjectDestroyedWhilePooled_IsSkipped()
        {
            GameObjectPool<Transform> pool = new(CreateObject);
            Transform destroyed = CreateObject();
            pool.Enqueue(destroyed);
            Object.DestroyImmediate(destroyed.gameObject);

            Transform obj = pool.Dequeue();

            Assert.IsTrue(obj != null);
            Assert.AreNotSame(destroyed, obj);
            Assert.AreEqual(0, pool.Count);
        }

        [Test]
        public void Enqueue_BeyondMaxSize_DestroysOverflow()
        {
            GameObjectPool<Transform> pool = new() { MaxSize = 1 };
            Transform first = CreateObject();
            Transform second = CreateObject();

            pool.Enqueue(first);
            pool.Enqueue(second);

            Assert.AreEqual(1, pool.Count);
            Assert.IsTrue(second == null);
        }

        [Test]
        public void Prewarm_FillsPool()
        {
            GameObjectPool<Transform> pool = new(CreateObject);

            pool.Prewarm(3);

            Assert.AreEqual(3, pool.Count);
        }

        [Test]
        public void Prewarm_RespectsMaxSize()
        {
            GameObjectPool<Transform> pool = new(CreateObject) { MaxSize = 2 };

            pool.Prewarm(5);

            Assert.AreEqual(2, pool.Count);
        }

        [Test]
        public void Enqueue_InvokesReturnHook()
        {
            Transform returned = null;
            GameObjectPool<Transform> pool = new(CreateObject, null, obj => returned = obj);
            Transform obj = CreateObject();

            pool.Enqueue(obj);

            Assert.AreSame(obj, returned);
        }

        [Test]
        public void Dequeue_InvokesInitializeHook()
        {
            Transform initialized = null;
            GameObjectPool<Transform> pool = new(CreateObject, obj => initialized = obj);

            Transform obj = pool.Dequeue();

            Assert.AreSame(obj, initialized);
        }

        [Test]
        public void Clear_DestroysPooledObjects()
        {
            GameObjectPool<Transform> pool = new();
            Transform obj = CreateObject();
            pool.Enqueue(obj);

            pool.Clear();

            Assert.AreEqual(0, pool.Count);
            Assert.IsTrue(obj == null);
        }
    }
}
