using UnityEngine;
using UnityEngine.Serialization;
using System;

namespace Mu3Library.DI
{
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
                }

                return m_coreRoot;
            }
        }

        [FormerlySerializedAs("_setAsGlobal")]
        [SerializeField] private bool _dontDestroyOnLoad = false;

        private readonly Container _container = new Container();
        private ContainerScope _scope;

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

            EnsureScope();

        }

        protected virtual void Start()
        {
            _coreRoot?.RegisterCore(this);
        }

        protected virtual void OnDestroy()
        {
            _coreRoot?.UnregisterCore(this);
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
        /// Return class by container
        /// </summary>
        protected T GetClass<T>()
            where T : class
        {
            EnsureScope();

            if (_scope.TryResolve(out T instance))
            {
                return instance;
            }

            return null;
        }

        /// <summary>
        /// Register class to container
        /// </summary>
        protected void RegisterClass<T>()
            where T : class, new()
        {
            EnsureScope();
            _scope.Register<T>(ServiceLifetime.Singleton);
        }

        private void EnsureScope()
        {
            if (_scope != null)
            {
                return;
            }

            EnsureCoreRoot();

            _scope = _container.CreateScope();
            ConfigureContainer(_scope);
        }

        private void EnsureCoreRoot()
        {
            if (_coreRoot != null)
            {
                return;
            }

            GameObject instance = new GameObject("CoreRoot");
            instance.AddComponent<CoreRoot>();
            m_coreRoot = instance.GetComponent<CoreRoot>();
        }
    }
}
