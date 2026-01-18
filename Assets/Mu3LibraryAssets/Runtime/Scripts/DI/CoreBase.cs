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

            // Fill container ignore types

            // Fill container classes

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
            _container?.Initialize();
        }

        internal void UpdateCore()
        {
            _container?.Update();
        }

        internal void LateUpdateCore()
        {
            _container?.LateUpdate();
        }

        internal void DisposeCore()
        {
            _container?.Dispose();
        }

        #region Utility
        /// <summary>
        /// Return class by container
        /// </summary>
        internal T GetClass<T>()
            where T : class
        {
            return _container?.Get<T>();
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
            return _coreRoot?.Get<TCore, T>();
        }

        /// <summary>
        /// Register class to container
        /// </summary>
        protected void RegisterClass<T>()
            where T : class, new()
        {
            _container.Register<T>();
        }
    }
}
