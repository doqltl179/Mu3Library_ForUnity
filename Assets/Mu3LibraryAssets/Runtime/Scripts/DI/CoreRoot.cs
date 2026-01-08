using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.DI
{
    public sealed class CoreRoot : MonoBehaviour
    {
        private readonly Dictionary<Type, CoreBase> _cores = new();



        private void Awake()
        {
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
        public T Get<TCore, T>()
            where TCore : CoreBase
            where T : class
        {
            Type coreType = typeof(TCore);
            if (!_cores.TryGetValue(coreType, out CoreBase core))
            {
                Debug.LogError($"Core not found. type: {coreType.FullName}");
                return null;
            }

            return core.Get<T>();
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
                Debug.LogError($"Core is already exist. type: {type.FullName}");
                return;
            }

            core.InitializeCore();
#if MU3LIBRARY_UNITASK_SUPPORT
            await core.InitializeCoreAsync();
#endif
        }
        #endregion

    }
}
