using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Mu3Library.Utility.ObjectPool
{
    public class MonoPool<T> : IPool<T> where T : MonoBehaviour
    {
        public Type ObjectType => typeof(T);

        private readonly List<T> _pool = new();



        #region Utility
        public void Add(T obj)
        {
            if (!IsAvailableObject(obj))
            {
                return;
            }

            _pool.Add(obj);
        }

        public void AddAll(IEnumerable<T> objs)
        {
            _pool.AddRange(objs.Where(obj => IsAvailableObject(obj)));
        }

        public T Get()
        {
            T result = null;

            int idx = _pool.FindIndex(obj => IsAvailableObject(obj));
            if (idx >= 0)
            {
                result = _pool[idx];
                _pool.RemoveRange(0, idx + 1);
            }
            else
            {
                _pool.Clear();
            }

            return result;
        }

        public T[] GetAll()
        {
            T[] result = _pool.Where(obj => IsAvailableObject(obj)).ToArray();

            _pool.Clear();

            return result;
        }
        #endregion

        private bool IsAvailableObject(T obj)
        {
            return obj != null && obj.gameObject != null;
        }
    }
}