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
        public delegate void Initialize(T obj);
        public delegate void Return(T obj);

        private readonly Create _onCreate;
        private readonly Initialize _onInitialize;
        private readonly Return _onReturn;

        private int _maxSize = 0;
        /// <summary>
        /// Largest number of pooled objects kept. An object returned to a full pool is destroyed
        /// instead. 0 or less means unlimited.
        /// </summary>
        public int MaxSize
        {
            get => _maxSize;
            set => _maxSize = value;
        }

        /// <summary>
        /// Number of objects currently waiting in the pool.
        /// </summary>
        public int Count => _pool.Count;


        public GameObjectPool() : this(null, null, null)
        {
        }

        public GameObjectPool(Create onCreate)
            : this(onCreate, null, null)
        {
        }

        public GameObjectPool(Create onCreate, Initialize onInitialize)
            : this(onCreate, onInitialize, null)
        {
        }

        public GameObjectPool(Create onCreate, Initialize onInitialize, Return onReturn)
        {
            _onCreate = onCreate;
            _onInitialize = onInitialize;
            _onReturn = onReturn;
        }

        /// <summary>
        /// Fills the pool up to the requested number of waiting objects, so the first
        /// dequeues pay no instantiation cost. Needs the create delegate.
        /// </summary>
        public void Prewarm(int count)
        {
            if (_onCreate == null)
            {
                Debug.LogWarning("Prewarm needs a create delegate.");
                return;
            }

            if (_maxSize > 0)
            {
                count = Mathf.Min(count, _maxSize);
            }

            while (_pool.Count < count)
            {
                T obj = _onCreate();
                if (obj == null)
                {
                    Debug.LogWarning("Create delegate returned null. Prewarm stopped.");
                    return;
                }

                Enqueue(obj);
            }
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

            _onReturn?.Invoke(obj);

            // A full pool keeps nothing extra; the returned object is destroyed after the
            // return hook had its chance to detach whatever it owns.
            if (_maxSize > 0 && _pool.Count >= _maxSize)
            {
                DestroyPooledObject(obj.gameObject);
                return;
            }

            _instanceIds.Add(instanceId);
            _pool.Enqueue(pooledItem);
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

            if (result != null)
            {
                _onInitialize?.Invoke(result);
            }

            return result;
        }

        public List<T> Dequeue(int count)
        {
            List<T> objects = new();
            if (count <= 0)
            {
                return objects;
            }

            while (objects.Count < count)
            {
                T obj = Dequeue();
                if (obj != null)
                {
                    objects.Add(obj);
                }
                else
                {
                    break;
                }
            }

            return objects;
        }

        public void Clear()
        {
            while (_pool.Count > 0)
            {
                PooledItem pooledItem = _pool.Dequeue();
                T obj = pooledItem.Object;
                if (obj != null)
                {
                    DestroyPooledObject(obj.gameObject);
                }
            }

            _instanceIds.Clear();
        }

        private static void DestroyPooledObject(GameObject go)
        {
            // Edit mode, which the tests run in, forbids the deferred destroy.
            if (Application.isPlaying)
            {
                Object.Destroy(go);
            }
            else
            {
                Object.DestroyImmediate(go);
            }
        }
    }



    public class GameObjectPool<T, TArgs> : GameObjectPool<T>
        where T : Component
        where TArgs : CreateArguments
    {
        public delegate void InitializeWithArguments(T obj, TArgs args);

        private readonly InitializeWithArguments _onInitializeWithArguments;



        public GameObjectPool() : this(null, null)
        {
        }

        public GameObjectPool(GameObjectPool<T>.Create onCreate)
            : this(onCreate, null)
        {
        }

        public GameObjectPool(
            GameObjectPool<T>.Create onCreate,
            InitializeWithArguments onInitialize)
            : base(onCreate)
        {
            _onInitializeWithArguments = onInitialize;
        }

        public T Dequeue(TArgs args)
        {
            T result = base.Dequeue();
            if (result != null)
            {
                _onInitializeWithArguments?.Invoke(result, args);
            }

            return result;
        }

        public List<T> Dequeue(int count, TArgs args)
        {
            List<T> objects = new();
            if (count <= 0)
            {
                return objects;
            }

            while (objects.Count < count)
            {
                T obj = Dequeue(args);
                if (obj != null)
                {
                    objects.Add(obj);
                }
                else
                {
                    break;
                }
            }

            return objects;
        }

        public List<T> Dequeue(List<TArgs> argsList)
        {
            List<T> objects = new();
            if (argsList == null)
            {
                Debug.LogWarning("Trying to dequeue with a null argument list from the pool.");
                return objects;
            }

            foreach (TArgs args in argsList)
            {
                T obj = Dequeue(args);
                if (obj != null)
                {
                    objects.Add(obj);
                }
            }

            return objects;
        }
    }
}
