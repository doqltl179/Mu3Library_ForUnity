using UnityEngine;
using UnityEngine.Serialization;
using System;
using Mu3Library.Foundation.Event;
using System.Collections;
using Mu3Library.Foundation.Coroutine;


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

        private bool _isPrepareRequested = false;

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
        // Moves on whenever a preparation is stopped, so a preparation stopped while it runs its
        // own step can tell, once that step returns, that it is no longer the current one.
        private int _prepareCoreGeneration = 0;
        private bool _isPrepareInterrupted = false;
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
#if UNITY_6000_4_OR_NEWER
                int instanceCount = FindObjectsByType(instanceType, FindObjectsInactive.Include).Length;
#else
                int instanceCount = FindObjectsByType(instanceType, FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
#endif
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

        internal void InitializeContainer()
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

            _isPrepareRequested = true;
            PrepareCore();
        }

        /// <summary>
        /// Retries a preparation that could not start yet, which is what a core that was disabled
        /// or whose object was inactive while <see cref="Start"/> ran is left with on the
        /// coroutine path, or that <see cref="OnDisable"/> stopped there; the latter logs one
        /// warning.
        /// </summary>
        /// <remarks>
        /// Derived classes that override this method must call <c>base.OnEnable()</c> to preserve the core lifecycle.
        /// </remarks>
        protected virtual void OnEnable()
        {
            if (_isPrepareRequested && !_isPrepared && !_isPreparing)
            {
#if !MU3LIBRARY_UNITASK_SUPPORT
                if (_isPrepareInterrupted)
                {
                    _isPrepareInterrupted = false;
                    Debug.LogWarning($"Core prepare was interrupted because the core was disabled or its object deactivated, and starts again. type: {GetType().Name}", this);
                }
#endif

                PrepareCore();
            }
        }

        /// <summary>
        /// Stops and rewinds a preparation on the coroutine path when this core is disabled or its
        /// object deactivated, so that <see cref="OnEnable"/> starts it again from the beginning.
        /// </summary>
        /// <remarks>
        /// Derived classes that override this method must call <c>base.OnDisable()</c> to preserve the core lifecycle.
        /// </remarks>
        protected virtual void OnDisable()
        {
#if !MU3LIBRARY_UNITASK_SUPPORT
            // Unity stops the coroutine when the object is deactivated and leaves it running when
            // only the component is disabled, but deactivating the object after that stops it
            // with no OnDisable left to rewind it, and the core would wait on it for good. So
            // both stop the preparation here. The warning waits for OnEnable, so a core destroyed
            // while it prepares does not announce a restart that never comes.
            if (_prepareCoreCoroutine != null)
            {
                StopPrepareCore();
                _isPreparing = false;
                _isPrepareInterrupted = true;
            }
#endif
        }

        /// <summary>
        /// Stops a running preparation and takes this core out of <see cref="CoreRoot"/>, which
        /// disposes its scope.
        /// </summary>
        /// <remarks>
        /// Derived classes that override this method must call <c>base.OnDestroy()</c> to preserve the core lifecycle.
        /// </remarks>
        protected virtual void OnDestroy()
        {
            StopPrepareCore();

            _subscribeHandler.Dispose();
            // Null once the application quits and the root is gone: the accessor no longer
            // creates a root then.
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
            // OnEnable starts what a disabled or inactive core could not start here.
            if (_prepareCoreCoroutine == null && enabled && gameObject.activeInHierarchy)
            {
                _prepareCoreCoroutine = PrepareCoreHandler(_prepareCoreGeneration);
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

        protected virtual UniTask<bool> PrepareCoreAsync(CancellationToken token)
        {
            return UniTask.FromResult(true);
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
        private IEnumerator PrepareCoreHandler(int generation)
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

            // PrepareCoreAsync and each step of its routine may disable this core or its object.
            // That stops this preparation, and one that OnEnable started since then is the
            // current one, so a stopped preparation leaves the state alone.
            if (generation != _prepareCoreGeneration)
            {
                yield break;
            }

            if (error == null)
            {
                // The routine and the routines it yields are stepped here instead of being handed
                // to Unity, which does not document whether stopping this coroutine also stops a
                // routine it yielded. Stepped here, they stop with this coroutine, and each step
                // returns here, which is why the generation is checked after it.
                var routines = new System.Collections.Generic.Stack<IEnumerator>();
                routines.Push(CoroutineSafeRunner.Run(routine, exception => error = exception));

                while (routines.Count > 0)
                {
                    IEnumerator current = routines.Peek();
                    bool hasNext = current.MoveNext();

                    if (generation != _prepareCoreGeneration)
                    {
                        yield break;
                    }

                    if (!hasNext)
                    {
                        routines.Pop();
                    }
                    else if (current.Current is IEnumerator nested)
                    {
                        routines.Push(nested);
                    }
                    else
                    {
                        yield return current.Current;
                    }
                }
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

        /// <summary>
        /// Override to prepare this core, and call <paramref name="onSucceed"/> once it is ready.
        /// </summary>
        /// <remarks>
        /// The core steps the returned routine and every routine it yields itself. Disabling the
        /// core or deactivating its object abandons them without running their <c>finally</c>
        /// blocks, and enabling the core again calls this method anew. A coroutine the routine
        /// starts with <c>StartCoroutine</c>, or an asynchronous operation it waits on, is not
        /// abandoned with them: disabling only the component leaves it running.
        /// </remarks>
        protected virtual IEnumerator PrepareCoreAsync(Action onSucceed)
        {
            onSucceed?.Invoke();
            yield break;
        }

        private void StopPrepareCore()
        {
            if (_prepareCoreCoroutine != null)
            {
                _prepareCoreGeneration++;
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

        #region Diagnostics
        /// <summary>
        /// Every descriptor this core's container holds, for diagnostics tooling.
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<ServiceDescriptor> GetRegisteredDescriptors()
        {
            return _container != null
                ? _container.GetAllDescriptors()
                : Array.Empty<ServiceDescriptor>();
        }

        /// <summary>
        /// Whether a service/key pair is registered in this core's container, without resolving.
        /// </summary>
        public bool IsServiceRegistered(Type serviceType, string key = null)
        {
            return _container != null && _container.IsRegistered(serviceType, key);
        }

        /// <summary>
        /// The singleton instances this core's container created so far, for diagnostics tooling.
        /// </summary>
        public System.Collections.Generic.IReadOnlyCollection<object> GetActiveSingletonInstances()
        {
            return _container != null
                ? _container.GetActiveSingletonInstances()
                : Array.Empty<object>();
        }
        #endregion

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
            if (_scope != null && _scope.TryResolve(out T instance))
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
            return _scope?.Resolve<T>(key);
        }

        /// <summary>
        /// Register a concrete type as singleton (self + interfaces) and immediately resolve it to trigger member injection and lifecycle tracking.
        /// </summary>
        protected void RegisterClass<T>()
            where T : class, new()
        {
            if (_scope == null)
            {
                Debug.LogError($"Container is not initialized yet. Register from ConfigureContainer() or later. type: {GetType().Name}");
                return;
            }

            _scope.Register<T>(ServiceLifetime.Singleton);
            // Immediately resolve to ensure lifecycle tracking (IInitializable, IUpdatable, etc.)
            _scope.Resolve<T>();
        }
    }
}
