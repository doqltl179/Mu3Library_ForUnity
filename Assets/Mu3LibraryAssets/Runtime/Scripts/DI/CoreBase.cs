using UnityEngine;
using System;

namespace Mu3Library.DI
{
    public abstract class CoreBase : MonoBehaviour
    {
        private CoreRoot m_coreRoot;
        protected CoreRoot _coreRoot
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

        [SerializeField] private bool _setAsGlobal = false;

        private readonly Container _container = new Container();



        protected virtual void Awake()
        {
            if (_setAsGlobal)
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
            _coreRoot?.RemoveCore(this);
        }

        #region Utility
        internal T Get<T>() where T : class
        {
            return _container?.Get<T>();
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
        #endregion

        protected void Register<T>()
            where T : class, new()
        {
            _container.Register<T>();
        }

        protected void WaitForCore<TCore>(Action<TCore> onReady)
            where TCore : CoreBase
        {
            if (_coreRoot == null)
            {
                return;
            }

            if (_coreRoot.TryGetCore(out TCore core))
            {
                onReady?.Invoke(core);
                return;
            }

            void HandleCoreAdded(Type addedType)
            {
                if (addedType != typeof(TCore))
                {
                    return;
                }

                if (_coreRoot.TryGetCore(out TCore readyCore))
                {
                    _coreRoot.OnCoreAdded -= HandleCoreAdded;

                    onReady?.Invoke(readyCore);
                }
            }

            _coreRoot.OnCoreAdded += HandleCoreAdded;
        }

        protected T GetFromCore<TCore, T>()
            where TCore : CoreBase
            where T : class
        {
            return _coreRoot?.Get<TCore, T>();
        }
    }
}
