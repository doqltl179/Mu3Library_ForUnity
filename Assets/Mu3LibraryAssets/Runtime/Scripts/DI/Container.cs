using System;
using System.Collections.Generic;
using UnityEngine;

#if MU3LIBRARY_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif

namespace Mu3Library.DI
{
    public class Container : IContainer
    {
        private readonly Dictionary<Type, object> _classMap = new();
        private readonly Dictionary<Type, object> _interfaceMap = new();

        private readonly HashSet<Type> _lifecycleTypes = new()
        {
            typeof(IInitializable),
            typeof(IDisposable),
            typeof(IUpdatable),
            typeof(ILateUpdatable),
#if MU3LIBRARY_UNITASK_SUPPORT
            typeof(IInitializableAsync),
            typeof(IDisposableAsync),
#endif
        };

        private readonly HashSet<Type> _interfaceIgnoreTypes = new()
        {

        };

        private readonly Dictionary<Type, IInitializable> _initializeMap = new();
        private readonly Dictionary<Type, IDisposable> _disposeMap = new();
        private readonly Dictionary<Type, IUpdatable> _updateMap = new();
        private readonly Dictionary<Type, ILateUpdatable> _lateUpdateMap = new();
#if MU3LIBRARY_UNITASK_SUPPORT
        private readonly Dictionary<Type, IInitializableAsync> _initializeAsyncMap = new();
        private readonly Dictionary<Type, IDisposableAsync> _disposeAsyncMap = new();
#endif



        #region Utility

#if MU3LIBRARY_UNITASK_SUPPORT
        public async UniTask DisposeAsync()
        {
            foreach (var obj in _disposeAsyncMap.Values)
            {
                await obj.DisposeAsync();
            }
        }

        public async UniTask InitializaAsync()
        {
            foreach (var obj in _initializeAsyncMap.Values)
            {
                await obj.InitializeAsync();
            }
        }
#endif

        public void LateUpdate()
        {
            foreach (var obj in _lateUpdateMap.Values)
            {
                obj.LateUpdate();
            }
        }

        public void Update()
        {
            foreach (var obj in _updateMap.Values)
            {
                obj.Update();
            }
        }

        public void Dispose()
        {
            foreach (var obj in _disposeMap.Values)
            {
                obj.Dispose();
            }
        }

        public void Initialze()
        {
            foreach (var obj in _initializeMap.Values)
            {
                obj.Initialize();
            }
        }

        /// <summary>
        /// Return interface by registered instance
        /// </summary>
        public T Get<T>()
            where T : class
        {
            Type type = typeof(T);
            if (!type.IsInterface)
            {
                Debug.LogError($"Requested type is not interface. type: {type.FullName}");
                return null;
            }

            if (_interfaceMap.TryGetValue(type, out object obj))
            {
                return obj as T;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Register interface by T instance
        /// </summary>
        public void Register<T>()
            where T : class, new()
        {
            Type type = typeof(T);

            if (!_classMap.ContainsKey(type))
            {
                T instance = new T();
                _classMap[type] = instance;

                RegisterInterfaces(instance);
            }
            else
            {
                Debug.LogWarning($"Class is already registered.\ntype: {type.FullName}");
            }
        }

        public void RemoveInterfaceIgnoreType<T>()
            => RemoveInterfaceIgnoreType(typeof(T));

        public void RemoveInterfaceIgnoreType(Type type)
        {
            _interfaceIgnoreTypes.Remove(type);
        }

        public void AddInterfaceIgnoreType<T>()
            => AddInterfaceIgnoreType(typeof(T));

        public void AddInterfaceIgnoreType(Type type)
        {
            _interfaceIgnoreTypes.Add(type);
        }
        #endregion

        private void RegisterInterfaces<T>(T instance)
        {
            if (instance == null)
            {
                return;
            }

            Type type = typeof(T);

            var interfaces = type.GetInterfaces();
            foreach (Type iType in interfaces)
            {
                if (_interfaceIgnoreTypes.Contains(iType))
                {
                    continue;
                }

                if (iType == typeof(IInitializable))
                {
                    _initializeMap[type] = instance as IInitializable;
                }
                else if (iType == typeof(IDisposable))
                {
                    _disposeMap[type] = instance as IDisposable;
                }
                else if (iType == typeof(IUpdatable))
                {
                    _updateMap[type] = instance as IUpdatable;
                }
                else if (iType == typeof(ILateUpdatable))
                {
                    _lateUpdateMap[type] = instance as ILateUpdatable;
                }
#if MU3LIBRARY_UNITASK_SUPPORT
                else if (iType == typeof(IInitializableAsync))
                {
                    _initializeAsyncMap[type] = instance as IInitializableAsync;
                }
                else if (iType == typeof(IDisposableAsync))
                {
                    _disposeAsyncMap[type] = instance as IDisposableAsync;
                }
#endif
                else
                {
                    _interfaceMap[iType] = instance;
                }
            }
        }
    }
}
