using UnityEngine;
using System.Collections.Generic;
using System;

namespace Mu3Library.Base.Utility.ObjectPool
{
    public static class PoolManager
    {
        private static readonly Dictionary<Type, object> _monoPools = new();



        #region Utility
        public static void Add<T>(T obj) where T : MonoBehaviour
        {
            GetPool<T>().Add(obj);
        }

        public static void AddAll<T>(IEnumerable<T> objs) where T : MonoBehaviour
        {
            GetPool<T>().AddAll(objs);
        }

        public static T Get<T>() where T : MonoBehaviour
        {
            return GetPool<T>().Get();
        }

        public static T[] GetAll<T>() where T : MonoBehaviour
        {
            return GetPool<T>().GetAll();
        }
        #endregion

        private static IPool<T> GetPool<T>() where T : MonoBehaviour
        {
            IPool<T> result = null;

            Type type = typeof(T);
            if (_monoPools.ContainsKey(type))
            {
                result = (IPool<T>)_monoPools[type];
            }
            else
            {
                result = new MonoPool<T>();
                _monoPools.Add(type, result);
            }

            return result;
        }
    }
}