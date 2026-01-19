using UnityEngine;
using UnityEngine.Serialization;
using System;

namespace Mu3Library.DI
{
    /// <summary>
    /// Base class for cores that own a DI scope and register services.
    /// </summary>
    public abstract class CoreBase : MonoBehaviour
    {
        private CoreRoot m_coreRoot;
        private CoreRoot _coreRoot
        {
            get
            {
                if (m_coreRoot == null)
                {
                    m_coreRoot = FindFirstObjectByType<CoreRoot>();

                    if (m_coreRoot == null)
                    {
                        GameObject instance = new GameObject("CoreRoot");
                        m_coreRoot = instance.AddComponent<CoreRoot>();
                    }
                }

                return m_coreRoot;
            }
        }

        private ContainerScope m_scope;
        private ContainerScope _scope
        {
            get
            {
                if (m_scope == null)
                {
                    m_scope = _container.CreateScope();

                    ConfigureContainer(m_scope);
                }

                return m_scope;
            }
        }

        private readonly Container _container = new Container();

        [FormerlySerializedAs("_setAsGlobal")]
        [SerializeField] private bool _dontDestroyOnLoad = false;



        /// <summary>
        /// Override to register services for this core.
        /// </summary>
        protected virtual void ConfigureContainer(ContainerScope scope)
        {
        }

        protected virtual void Awake()
        {
            if (_dontDestroyOnLoad)
            {
                Type instanceType = GetType();
                int instanceCount = FindObjectsByType(instanceType, FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                if (instanceCount > 1)
                {
                    Debug.LogWarning($"Core is set as global, but multiple instances of {instanceType.Name} detected. There should only be one in the scene.", this);

                    Destroy(gameObject);
                    return;
                }

                DontDestroyOnLoad(gameObject);
            }
        }

        protected virtual void Start()
        {
            _coreRoot.RegisterCore(this);
        }

        protected virtual void OnDestroy()
        {
            _coreRoot.UnregisterCore(this);
        }

        internal void InitializeCore()
        {
            _scope?.Initialize();
        }

        internal void UpdateCore()
        {
            _scope?.Update();
        }

        internal void LateUpdateCore()
        {
            _scope?.LateUpdate();
        }

        internal void DisposeCore()
        {
            _scope?.Dispose();
        }

        #region Utility
        /// <summary>
        /// Return class by container
        /// </summary>
        internal T GetClassFromContainer<T>()
            where T : class
        {
            return GetClass<T>();
        }
        #endregion

        protected void WaitForOtherCore<TCore>(Action onReady)
            where TCore : CoreBase
        {
            if (_coreRoot == null)
            {
                return;
            }

            if (_coreRoot.HasCore<TCore>())
            {
                onReady?.Invoke();
                return;
            }

            void HandleCoreAdded(Type addedType)
            {
                if (addedType != typeof(TCore))
                {
                    return;
                }

                if (_coreRoot.HasCore<TCore>())
                {
                    _coreRoot.OnCoreAdded -= HandleCoreAdded;

                    onReady?.Invoke();
                }
            }

            _coreRoot.OnCoreAdded += HandleCoreAdded;
        }

        protected T GetClassFromOtherCore<TCore, T>()
            where TCore : CoreBase
            where T : class
        {
            return _coreRoot?.GetClass<TCore, T>();
        }

        /// <summary>
        /// Resolve a service from this core's scope.
        /// </summary>
        protected T GetClass<T>()
            where T : class
        {
            if (_scope.TryResolve(out T instance))
            {
                return instance;
            }

            return null;
        }

        /// <summary>
        /// Resolve a service from this core's scope with a key.
        /// </summary>
        protected T GetClass<T>(string key)
            where T : class
        {
            return _scope.Resolve<T>(key);
        }

        /// <summary>
        /// Register a concrete type as singleton (self + interfaces).
        /// </summary>
        protected void RegisterClass<T>()
            where T : class, new()
        {
            _scope.Register<T>(ServiceLifetime.Singleton);
        }
    }
}
