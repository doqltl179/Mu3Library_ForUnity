using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.DI
{
    public class Container
    {
        private readonly Dictionary<Type, object> _classMap = new();
        private readonly Dictionary<Type, object> _interfaceMap = new();

        private readonly HashSet<Type> _lifecycleTypes = new()
        {
            typeof(IInitializable),
            typeof(IDisposable),
            typeof(IUpdatable),
            typeof(ILateUpdatable),
        };

        private readonly HashSet<Type> _interfaceIgnoreTypes = new()
        {

        };

        private readonly Dictionary<Type, IInitializable> _initializeMap = new();
        private readonly Dictionary<Type, IDisposable> _disposeMap = new();
        private readonly Dictionary<Type, IUpdatable> _updateMap = new();
        private readonly Dictionary<Type, ILateUpdatable> _lateUpdateMap = new();



        #region Utility

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

        public void Initialize()
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
                else
                {
                    _interfaceMap[iType] = instance;
                }
            }
        }
    }
}
