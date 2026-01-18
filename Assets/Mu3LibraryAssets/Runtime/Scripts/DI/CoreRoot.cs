using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.DI
{
    public sealed class CoreRoot : MonoBehaviour
    {
        private readonly Dictionary<Type, CoreBase> _cores = new();
        internal event Action<Type> OnCoreAdded;



        private void Awake()
        {
            int instanceCount = FindObjectsByType<CoreRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            if (instanceCount > 1)
            {
                Debug.LogWarning("Multiple instances of CoreRoot detected. There should only be one CoreRoot in the project.");

                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            var cores = new List<CoreBase>(_cores.Values);
            foreach (var core in cores)
            {
                if (core == null)
                {
                    continue;
                }

                UnregisterCore(core);
            }
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
