using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.ObjectPool
{
    public class GameObjectPool<T> where T : Component
    {
        private System.Type m_type;
        private System.Type _type
        {
            get
            {
                if (m_type == null)
                {
                    m_type = typeof(T);
                }

                return m_type;
            }
        }
        public System.Type Type => _type;

        private readonly List<T> _pool = new();

        private readonly T _resource;



        public GameObjectPool(T resource)
        {
            if(resource == null)
            {
                Debug.LogError("Resource provided to GameObjectPool is null.");
            }

            _resource = resource;
        }

        public void Enqueue(T obj)
        {
            if(obj == null)
            {
                Debug.LogWarning("Trying to enqueue a null object to the pool.");
                return;
            }

            _pool.Add(obj);
        }

        public T Dequeue()
        {
            if (_resource == null)
            {
                Debug.LogError("Resource is null. Cannot instantiate new objects.");
                return null;
            }

            T result = null;

            int idx = _pool.FindIndex(obj => obj != null);
            if (idx >= 0)
            {
                result = _pool[idx];
                _pool.RemoveRange(0, idx + 1);
            }
            else
            {
                result = Object.Instantiate(_resource);
            }

            return result;
        }
    }
}