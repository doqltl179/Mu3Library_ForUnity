using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.ObjectPool
{
    public class GameObjectPool<T> where T : Component
    {
        private readonly struct PooledItem
        {
            public PooledItem(T obj)
            {
                Object = obj;
                InstanceId = obj.GetInstanceID();
            }

            public T Object { get; }

            public int InstanceId { get; }
        }

        private System.Type m_type;
        private System.Type _type => m_type ??= typeof(T);
        public System.Type Type => _type;

        private readonly Queue<PooledItem> _pool = new();
        private readonly HashSet<int> _instanceIds = new();

        public delegate T Create();
        private readonly Create _onCreate;



        public GameObjectPool() : this(null)
        {
        }

        public GameObjectPool(Create onCreate)
        {
            _onCreate = onCreate;
        }

        public void Enqueue(T obj)
        {
            if (obj == null)
            {
                Debug.LogWarning("Trying to enqueue a null object to the pool.");
                return;
            }

            int instanceId = obj.GetInstanceID();
            if (_instanceIds.Contains(instanceId))
            {
                Debug.LogWarning($"Object with instance ID {instanceId} is already in the pool. Skipping enqueue.");
                return;
            }

            _pool.Enqueue(new PooledItem(obj));
            _instanceIds.Add(instanceId);
        }

        public T Dequeue()
        {
            T result = null;

            while (_pool.Count > 0)
            {
                PooledItem pooledItem = _pool.Dequeue();
                _instanceIds.Remove(pooledItem.InstanceId);

                result = pooledItem.Object;
                if (result != null)
                {
                    break;
                }
            }

            if (result == null && _onCreate != null)
            {
                result = _onCreate();
            }

            return result;
        }

        public void Clear()
        {
            while (_pool.Count > 0)
            {
                PooledItem pooledItem = _pool.Dequeue();
                T obj = pooledItem.Object;
                if (obj != null)
                {
                    Object.Destroy(obj.gameObject);
                }
            }

            _instanceIds.Clear();
        }
    }
}