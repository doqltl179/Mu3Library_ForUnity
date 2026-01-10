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

        private void OnDestroy()
        {
            var cores = new List<CoreBase>(_cores.Values);
            foreach (var core in cores)
            {
                if (core == null)
                {
                    continue;
                }

                RemoveCore(core);
            }
        }

        #region Utility
        internal T Get<TCore, T>()
            where TCore : CoreBase
            where T : class
        {
            Type coreType = typeof(TCore);
            if (!_cores.TryGetValue(coreType, out CoreBase core))
            {
                Debug.LogError($"Core not found. type: {coreType.Name}");
                return null;
            }

            return core.Get<T>();
        }

        internal bool TryGetCore<TCore>(out TCore core)
            where TCore : CoreBase
        {
            core = null;
            
            if (_cores.TryGetValue(typeof(TCore), out CoreBase coreBase))
            {
                core = coreBase as TCore;
                return core != null;
            }

            return false;
        }

#if MU3LIBRARY_UNITASK_SUPPORT
        public async void RemoveCore<T>(T core) where T : CoreBase
#else
        public void RemoveCore<T>(T core) where T : CoreBase
#endif
        {
            if (core == null)
            {
                return;
            }

            Type type = core.GetType();
            if (_cores.Remove(type))
            {
                core.DisposeCore();
#if MU3LIBRARY_UNITASK_SUPPORT
                await core.DisposeCoreAsync();
#endif
            }
        }

#if MU3LIBRARY_UNITASK_SUPPORT
        public async void AddCore<T>(T core) where T : CoreBase
#else
        public void AddCore<T>(T core) where T : CoreBase
#endif
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
#if MU3LIBRARY_UNITASK_SUPPORT
            await core.InitializeCoreAsync();
#endif

            OnCoreAdded?.Invoke(type);
        }
        #endregion

    }
}
