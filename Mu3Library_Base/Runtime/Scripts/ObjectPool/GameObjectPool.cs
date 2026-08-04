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

        private static readonly System.Type ComponentType = typeof(T);
        public System.Type Type => ComponentType;

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

            PooledItem pooledItem = new(obj);
            int instanceId = pooledItem.InstanceId;
            if (_instanceIds.Contains(instanceId))
            {
                Debug.LogWarning($"Object with instance ID {instanceId} is already in the pool. Skipping enqueue.");
                return;
            }

            _pool.Enqueue(pooledItem);
            _instanceIds.Add(instanceId);
        }

        public void Enqueue(List<T> objects)
        {
            if (objects == null)
            {
                Debug.LogWarning("Trying to enqueue a null list to the pool.");
                return;
            }

            foreach (T obj in objects)
            {
                Enqueue(obj);
            }
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

        public void Dequeue(List<T> objects)
        {
            if (objects == null)
            {
                Debug.LogWarning("Trying to dequeue into a null list from the pool.");
                return;
            }

            while (_pool.Count > 0)
            {
                PooledItem pooledItem = _pool.Dequeue();
                _instanceIds.Remove(pooledItem.InstanceId);

                T obj = pooledItem.Object;
                if (obj != null)
                {
                    objects.Add(obj);
                }
            }
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



    public class GameObjectPool<T, TArgs> : GameObjectPool<T>
        where T : Component
        where TArgs : CreateArguments
    {
        public delegate T CreateWithArguments(TArgs args);

        private readonly CreateWithArguments _onCreateWithArguments;



        public GameObjectPool() : this(null)
        {
        }

        public GameObjectPool(CreateWithArguments onCreate)
        {
            _onCreateWithArguments = onCreate;
        }

        public T Dequeue(TArgs args)
        {
            T result = base.Dequeue();
            if (result == null && _onCreateWithArguments != null)
            {
                result = _onCreateWithArguments(args);
            }

            return result;
        }
    }
}
