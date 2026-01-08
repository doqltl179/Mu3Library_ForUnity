using UnityEngine;
#if MU3LIBRARY_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif

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

        protected readonly IContainer _container = new Container();



        protected virtual void Awake()
        {
            // Fill container ignore types

            // Fill container classes
            
        }

        protected virtual void Start()
        {
            _coreRoot?.AddCore(this);
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
            _container?.Initialze();
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

#if MU3LIBRARY_UNITASK_SUPPORT
        internal UniTask InitializeCoreAsync()
        {
            if (_container == null)
            {
                return UniTask.CompletedTask;
            }

            return _container.InitializaAsync();
        }

        internal UniTask DisposeCoreAsync()
        {
            if (_container == null)
            {
                return UniTask.CompletedTask;
            }

            return _container.DisposeAsync();
        }
#endif
        #endregion
    }
}
