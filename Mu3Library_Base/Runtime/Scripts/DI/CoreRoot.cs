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
                    // A core torn down while the application quits must not bring a new root
                    // to life, which would leave a ghost object behind once play mode ends.
                    if (_isShuttingDown)
                    {
                        return null;
                    }

                    lock (_lockObj)
                    {
                        // Double-checked locking
                        if (m_instance == null)
                        {
#if UNITY_6000_4_OR_NEWER
                            var instances = FindObjectsByType<CoreRoot>();
#else
                            var instances = FindObjectsByType<CoreRoot>(FindObjectsSortMode.None);
#endif
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

        // Mirrors SingletonRuntimeState.IsShuttingDown. That flag is internal to the Mu3Library
        // assembly, which references this one, so this assembly cannot read it; widening it
        // would expose the whole Mu3Library internal surface to the DI assembly for one bool.
        private static bool _isShuttingDown = false;

        private readonly Dictionary<Type, CoreBase> _cores = new();
        private readonly List<CoreBase> _orderedCores = new();

        // A core that registers or leaves while the cores are walked changes the walk only once
        // the walk is over: new cores wait here for the next frame, and a leaving core's slot is
        // emptied in place and compacted afterwards.
        private readonly List<CoreBase> _pendingCores = new();
        private bool _isWalkingCores = false;
        private bool _hasVacatedSlots = false;

        /// <summary>
        /// The registered cores in execution order, for diagnostics.
        /// </summary>
        /// <remarks>
        /// This is the list the root updates. Read from inside a core's update, it shows a core
        /// that left during that update as a null entry, and it lists a core that registered
        /// during an update only from the next frame on.
        /// </remarks>
        public IReadOnlyList<CoreBase> RegisteredCores => _orderedCores;

        private readonly SubscribeHandler _subscribeHandler = new();
        public event Action<Type> OnCoreInitialized;
        public event Action<Type> OnCorePrepared;



        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetShutdownState()
        {
            _isShuttingDown = false;

            // Subscribing twice is what a domain reload that was turned off would cause.
            Application.quitting -= MarkShuttingDown;
            Application.quitting += MarkShuttingDown;
        }

        internal static void MarkShuttingDown()
        {
            _isShuttingDown = true;
        }

        private void OnDestroy()
        {
            _pendingCores.Clear();

            var cores = new List<CoreBase>(_cores.Values);
            foreach (var core in cores)
            {
                if (core == null)
                {
                    continue;
                }

                try
                {
                    UnregisterCore(core);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, core);
                }
            }

            _cores.Clear();
            _orderedCores.Clear();

            _subscribeHandler.Dispose();

            OnCoreInitialized = null;
            OnCorePrepared = null;
        }

        private void Update()
        {
            UpdateCores();
        }

        private void LateUpdate()
        {
            LateUpdateCores();
        }

        #region internal
        internal void UpdateCores()
        {
            // Cores that registered during the previous frame's walks join here.
            if (_pendingCores.Count > 0)
            {
                _orderedCores.AddRange(_pendingCores);
                _pendingCores.Clear();
                _orderedCores.Sort(CompareCoreOrder);
            }

            _isWalkingCores = true;
            for (int i = 0; i < _orderedCores.Count; i++)
            {
                CoreBase core = _orderedCores[i];
                if (ReferenceEquals(core, null))
                {
                    continue;
                }

                try
                {
                    core.UpdateCore();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, core);
                }
            }
            EndCoreWalk();
        }

        internal void LateUpdateCores()
        {
            _isWalkingCores = true;
            for (int i = 0; i < _orderedCores.Count; i++)
            {
                CoreBase core = _orderedCores[i];
                if (ReferenceEquals(core, null))
                {
                    continue;
                }

                try
                {
                    core.LateUpdateCore();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, core);
                }
            }
            EndCoreWalk();
        }

        internal void UnregisterCore<T>(T core) where T : CoreBase
        {
            if (core == null)
            {
                return;
            }

            // Only the registered instance may take its type out of the registry. A duplicate
            // core that was refused registration still tears itself down here, and it must not
            // drag the surviving core out of the registry on its way.
            Type type = core.GetType();
            if (_cores.TryGetValue(type, out CoreBase registered) && ReferenceEquals(registered, core))
            {
                _cores.Remove(type);
            }

            _pendingCores.Remove(core);
            if (_isWalkingCores)
            {
                int index = _orderedCores.IndexOf(core);
                if (index >= 0)
                {
                    _orderedCores[index] = null;
                    _hasVacatedSlots = true;
                }
            }
            else
            {
                _orderedCores.Remove(core);
            }

            core.DisposeCore();
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

            // A core created while the cores are walked, from a service's Update for example,
            // joins from the next frame: sorting it in now would make the running walk update
            // one core twice and skip another.
            if (_isWalkingCores)
            {
                _pendingCores.Add(core);
            }
            else
            {
                _orderedCores.Add(core);
                _orderedCores.Sort(CompareCoreOrder);
            }

            core.SubscribeOnInitializedOnce(() => OnCoreInitialized?.Invoke(type));
            try
            {
                core.InitializeCore();
            }
            catch (Exception ex)
            {
                // A throwing initialized handler must not keep the prepared notification
                // below from being wired up.
                Debug.LogException(ex, core);
            }

            core.SubscribeOnPreparedOnce(() => OnCorePrepared?.Invoke(type));
        }
        #endregion

        private void EndCoreWalk()
        {
            _isWalkingCores = false;

            if (_hasVacatedSlots)
            {
                _orderedCores.RemoveAll(core => ReferenceEquals(core, null));
                _hasVacatedSlots = false;
            }
        }

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

        public ISubscriptionInfo SubscribeOnCorePreparedOnce<T>(Action callback) where T : CoreBase
        {
            Type type = typeof(T);
            return SubscribeOnCorePreparedOnce(type, callback);
        }

        public ISubscriptionInfo SubscribeOnCorePreparedOnce(Type type, Action callback)
        {
            if (type == null)
            {
                return null;
            }

            if (_cores.TryGetValue(type, out var core))
            {
                return core.SubscribeOnPreparedOnce(callback);
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
                    () => OnCorePrepared += handler,
                    () => OnCorePrepared -= handler
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
