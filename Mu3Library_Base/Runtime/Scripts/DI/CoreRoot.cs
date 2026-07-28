using System;
using System.Collections.Generic;
using Mu3Library.Foundation.Event;
using UnityEngine;

namespace Mu3Library.DI
{
    public sealed class CoreRoot : MonoBehaviour, ICoreRoot
    {
        private static CoreRoot m_instance;
        private static CoreRoot _instance
        {
            get
            {
                if (m_instance == null)
                {
                    lock (_lockObj)
                    {
                        // Double-checked locking
                        if (m_instance == null)
                        {
                            var instances = FindObjectsByType<CoreRoot>(FindObjectsSortMode.None);
                            if (instances.Length == 0)
                            {
                                GameObject go = new GameObject(typeof(CoreRoot).Name);
                                m_instance = go.AddComponent<CoreRoot>();
                                DontDestroyOnLoad(m_instance.gameObject);
                            }
                            else if (instances.Length == 1)
                            {
                                m_instance = instances[0];
                                DontDestroyOnLoad(m_instance.gameObject);
                            }
                            else
                            {
                                Debug.LogWarning($"'{typeof(CoreRoot).Name}' already exist more than one. Cleaning up duplicates.");

                                m_instance = instances[0];
                                DontDestroyOnLoad(m_instance.gameObject);

                                for (int i = 1; i < instances.Length; i++)
                                {
                                    Destroy(instances[i].gameObject);
                                }
                            }
                        }
                    }
                }

                return m_instance;
            }
        }

        internal static CoreRoot InstanceInternal => _instance;
        public static ICoreRoot Instance => _instance;

        private static readonly object _lockObj = new object();

        private readonly Dictionary<Type, CoreBase> _cores = new();
        private readonly List<CoreBase> _orderedCores = new();

        private readonly SubscribeHandler _subscribeHandler = new();
        public event Action<Type> OnCoreInitialized;



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

            _cores.Clear();
            _orderedCores.Clear();
        }

        private void Update()
        {
            for (int i = 0; i < _orderedCores.Count; i++)
            {
                CoreBase core = _orderedCores[i];
                core?.UpdateCore();
            }
        }

        private void LateUpdate()
        {
            for (int i = 0; i < _orderedCores.Count; i++)
            {
                CoreBase core = _orderedCores[i];
                core?.LateUpdateCore();
            }
        }

        #region internal
        internal void UnregisterCore<T>(T core) where T : CoreBase
        {
            if (core == null)
            {
                return;
            }

            Type type = core.GetType();
            if (_cores.Remove(type))
            {
                _orderedCores.Remove(core);
                core.DisposeCore();
            }
        }

        internal void RegisterCore<T>(T core) where T : CoreBase
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

            _orderedCores.Add(core);
            _orderedCores.Sort(CompareCoreOrder);

            core.SubscribeOnInitializedOnce(() => OnCoreInitialized?.Invoke(type));
            core.InitializeCore();
        }
        #endregion

        #region Utility
        public T GetClass<TCore, T>()
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

        public object GetClass(Type coreType, Type serviceType, string key)
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

        public ISubscriptionInfo SubscribeOnCoreInitializedOnce<T>(Action callback) where T : CoreBase
        {
            Type type = typeof(T);
            return SubscribeOnCoreInitializedOnce(type, callback);
        }

        public ISubscriptionInfo SubscribeOnCoreInitializedOnce(Type type, Action callback)
        {
            if (type == null)
            {
                return null;
            }

            if (_cores.TryGetValue(type, out var core))
            {
                return core.SubscribeOnInitializedOnce(callback);
            }
            else
            {
                ISubscriptionInfo subscription = null;
                Action<Type> handler = initializedType =>
                {
                    if (initializedType != type)
                    {
                        return;
                    }

                    _subscribeHandler.Deregister(subscription);
                    callback?.Invoke();
                };

                subscription = _subscribeHandler.Register(
                    () => OnCoreInitialized += handler,
                    () => OnCoreInitialized -= handler
                );
                subscription?.Subscribe();

                return subscription;
            }
        }
        #endregion

        private int CompareCoreOrder(CoreBase a, CoreBase b)
        {
            if (ReferenceEquals(a, b))
            {
                return 0;
            }

            if (a == null)
            {
                return -1;
            }

            if (b == null)
            {
                return 1;
            }

            int orderCompare = a.ExecutionOrder.CompareTo(b.ExecutionOrder);
            if (orderCompare != 0)
            {
                return orderCompare;
            }

            return string.CompareOrdinal(a.GetType().FullName, b.GetType().FullName);
        }
    }
}
