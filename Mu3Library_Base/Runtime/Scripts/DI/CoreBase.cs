using UnityEngine;
using UnityEngine.Serialization;
using System;
using Mu3Library.Foundation.Event;
using System.Collections;
using Mu3Library.Coroutine.Foundation;


#if MU3LIBRARY_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace Mu3Library.DI
{
    /// <summary>
    /// Base class for cores that own a DI scope and register services.
    /// </summary>
    public abstract class CoreBase : MonoBehaviour, IDICore
    {
        private ContainerScope _scope;
        private Container _container;

        private bool _isContainerConfigured = false;
        public bool IsContainerConfigured => _isContainerConfigured;

        private bool _isInitialized = false;
        public bool IsInitialized => _isInitialized;

        private bool _isPreparing = false;
        public bool IsPreparing => _isPreparing;

        private bool _isPrepared = false;
        public bool IsPrepared => _isPrepared;

        [FormerlySerializedAs("_setAsGlobal")]
        [SerializeField] private bool _dontDestroyOnLoad = false;
        [SerializeField] private int _executionOrder = 0;
        public int ExecutionOrder => _executionOrder;

#if MU3LIBRARY_UNITASK_SUPPORT
        private CancellationTokenSource _prepareCoreCts = null;
#else
        private IEnumerator _prepareCoreCoroutine = null;
#endif

        private readonly SubscribeHandler _subscribeHandler = new();
        internal event Action OnInitialized;
        internal event Action OnPrepared;



        /// <summary>
        /// Override to register services for this core.
        /// </summary>
        protected virtual void ConfigureContainer()
        {
        }

        /// <summary>
        /// Initializes this core's container and registers it with <see cref="CoreRoot"/>.
        /// </summary>
        /// <remarks>
        /// Derived classes that override this method must call <c>base.Awake()</c> to preserve the core lifecycle.
        /// </remarks>
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

            InitializeContainer();
            CoreRoot.InstanceInternal?.RegisterCore(this);
        }

        private void InitializeContainer()
        {
            if (_isContainerConfigured)
            {
                return;
            }

            _container = new Container(this);
            _scope = _container.CreateScope();

            ConfigureContainer();

            _isContainerConfigured = true;
        }

        /// <summary>
        /// Injects dependencies into this core and starts its preparation lifecycle.
        /// </summary>
        /// <remarks>
        /// Derived classes that override this method must call <c>base.Start()</c> to preserve the core lifecycle.
        /// </remarks>
        protected virtual void Start()
        {
            _scope.InjectInto(this);
            PrepareCore();
        }

        protected virtual void OnDestroy()
        {
            StopPrepareCore();

            _subscribeHandler.Dispose();
            CoreRoot.InstanceInternal?.UnregisterCore(this);
        }

        internal void InitializeCore()
        {
            _scope?.Initialize();
            _isInitialized = true;

            OnInitialized?.Invoke();
        }

        internal void PrepareCore()
        {
            if (this == null || _isPreparing || _isPrepared)
            {
                return;
            }

#if MU3LIBRARY_UNITASK_SUPPORT
            if (_prepareCoreCts == null)
            {
                _prepareCoreCts = new CancellationTokenSource();
                PrepareCoreHandler(_prepareCoreCts.Token);
            }
#else
            if (_prepareCoreCoroutine == null && gameObject.activeSelf)
            {
                _prepareCoreCoroutine = PrepareCoreHandler();
                StartCoroutine(_prepareCoreCoroutine);
            }
#endif
        }

#if MU3LIBRARY_UNITASK_SUPPORT
        private async void PrepareCoreHandler(CancellationToken token)
        {
            _isPreparing = true;

            try
            {
                _isPrepared = await PrepareCoreAsync(token);
            }
            catch (OperationCanceledException)
            {
                Debug.LogWarning($"Core prepare canceled. type: {GetType().Name}");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                _prepareCoreCts?.Dispose();
                _prepareCoreCts = null;

                _isPreparing = false;
            }

            if (_isPrepared)
            {
                OnPrepared?.Invoke();
            }
        }

        protected async virtual UniTask<bool> PrepareCoreAsync(CancellationToken token)
        {
            return true;
        }

        private void StopPrepareCore()
        {
            if (_prepareCoreCts != null)
            {
                _prepareCoreCts.Cancel();
                _prepareCoreCts.Dispose();
                _prepareCoreCts = null;
            }
        }
#else
        private IEnumerator PrepareCoreHandler()
        {
            _isPreparing = true;

            Exception error = null;
            bool isSucceed = false;

            IEnumerator routine = null;
            try
            {
                routine = PrepareCoreAsync(() => isSucceed = true);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            if (error == null)
            {
                yield return CoroutineSafeRunner.Run(
                    routine,
                    exception => error = exception
                );
            }

            _isPreparing = false;
            _isPrepared = error == null && isSucceed;

            _prepareCoreCoroutine = null;

            if (_isPrepared)
            {
                OnPrepared?.Invoke();
            }
            else if (error != null)
            {
                Debug.LogException(error);
            }
        }

        protected virtual IEnumerator PrepareCoreAsync(Action onSucceed)
        {
            onSucceed?.Invoke();
            yield break;
        }

        private void StopPrepareCore()
        {
            if (_prepareCoreCoroutine != null)
            {
                StopCoroutine(_prepareCoreCoroutine);
                _prepareCoreCoroutine = null;
            }
        }
#endif

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

        internal object GetClassFromContainer(Type serviceType, string key = null)
        {
            if (serviceType == null || _scope == null)
            {
                return null;
            }

            return _scope.Resolve(serviceType, key);
        }

        internal ISubscriptionInfo SubscribeOnInitializedOnce(Action callback)
        {
            if (_isInitialized)
            {
                callback?.Invoke();
                return null;
            }

            return _subscribeHandler.SubscribeOnce(
                    handler => OnInitialized += handler,
                    handler => OnInitialized -= handler,
                    callback
                );
        }

        internal ISubscriptionInfo SubscribeOnPreparedOnce(Action callback)
        {
            if (_isPrepared)
            {
                callback?.Invoke();
                return null;
            }

            return _subscribeHandler.SubscribeOnce(
                    handler => OnPrepared += handler,
                    handler => OnPrepared -= handler,
                    callback
                );
        }
        #endregion

        protected T GetClassFromOtherCore<TCore, T>()
            where TCore : CoreBase
            where T : class
        {
            return CoreRoot.InstanceInternal?.GetClass<TCore, T>();
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
        /// Register a concrete type as singleton (self + interfaces) and immediately resolve it to trigger member injection and lifecycle tracking.
        /// </summary>
        protected void RegisterClass<T>()
            where T : class, new()
        {
            _scope.Register<T>(ServiceLifetime.Singleton);
            // Immediately resolve to ensure lifecycle tracking (IInitializable, IUpdatable, etc.)
            _scope.Resolve<T>();
        }
    }
}
