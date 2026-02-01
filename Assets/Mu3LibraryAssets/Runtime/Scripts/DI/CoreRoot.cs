using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.DI
{
    public sealed class CoreRoot : MonoBehaviour
    {
        private static CoreRoot _instance;
        private static bool _isQuitting = false;

        internal static CoreRoot Instance
        {
            get
            {
                if (_isQuitting)
                {
                    return null;
                }

                if (_instance == null)
                {
                    lock (_lockObj)
                    {
                        // Double-checked locking
                        if (_instance == null)
                        {
                            var instances = FindObjectsByType<CoreRoot>(FindObjectsSortMode.None);
                            if (instances.Length == 0)
                            {
                                GameObject go = new GameObject(typeof(CoreRoot).Name);
                                _instance = go.AddComponent<CoreRoot>();
                                DontDestroyOnLoad(_instance.gameObject);
                            }
                            else if (instances.Length == 1)
                            {
                                _instance = instances[0];
                                DontDestroyOnLoad(_instance.gameObject);
                            }
                            else
                            {
                                Debug.LogWarning($"'{typeof(CoreRoot).Name}' already exist more than one. Cleaning up duplicates.");

                                _instance = instances[0];
                                DontDestroyOnLoad(_instance.gameObject);

                                for (int i = 1; i < instances.Length; i++)
                                {
                                    Destroy(instances[i].gameObject);
                                }
                            }
                        }
                    }
                }

                return _instance;
            }
        }

        private static readonly object _lockObj = new object();

        private readonly Dictionary<Type, CoreBase> _cores = new();
        internal event Action<Type> OnCoreAdded;



        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            var cores = new List<CoreBase>(_cores.Values);
            foreach (var core in cores)
            {
                if (core == null)
                {
                    continue;
                }

                UnregisterCore(core);
            }

            _cores.Clear();
        }

        private void Update()
        {
            foreach (var core in _cores.Values)
            {
                core?.UpdateCore();
            }
        }

        private void LateUpdate()
        {
            foreach (var core in _cores.Values)
            {
                core?.LateUpdateCore();
            }
        }

        #region Utility
        internal T GetClass<TCore, T>()
            where TCore : CoreBase
            where T : class
        {
            Type coreType = typeof(TCore);
            if (!_cores.TryGetValue(coreType, out CoreBase core))
            {
                Debug.LogError($"Core not found. type: {coreType.Name}");
                return null;
            }

            return core.GetClassFromContainer<T>();
        }

        internal object GetClass(Type coreType, Type serviceType, string key)
        {
            if (coreType == null || serviceType == null)
            {
                return null;
            }

            if (!_cores.TryGetValue(coreType, out CoreBase core))
            {
                Debug.LogError($"Core not found. type: {coreType.Name}");
                return null;
            }

            return core.GetClassFromContainer(serviceType, key);
        }

        public bool HasCore<T>() where T : CoreBase
        {
            Type type = typeof(T);
            return _cores.ContainsKey(type);
        }

        public void UnregisterCore<T>(T core) where T : CoreBase
        {
            if (core == null)
            {
                return;
            }

            Type type = core.GetType();
            if (_cores.Remove(type))
            {
                core.DisposeCore();
            }
        }

        public void RegisterCore<T>(T core) where T : CoreBase
        {
            if (core == null)
            {
                return;
            }

            Type type = core.GetType();
            if (!_cores.TryAdd(type, core))
            {
                Debug.LogError($"Core is already exist. type: {type.Name}");
                return;
            }

            core.InitializeCore();

            OnCoreAdded?.Invoke(type);
        }
        #endregion

    }
}
