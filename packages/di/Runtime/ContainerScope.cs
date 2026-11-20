using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Mu3Library.DI
{
    /// <summary>
    /// Resolution scope that owns scoped instances and lifecycle callbacks.
    /// </summary>
    public sealed class ContainerScope : IDisposable, IObjectInjector
    {
        private readonly Container _container;
        private readonly RegistrationInstanceCache _scopedInstances = new();
        private readonly HashSet<object> _trackedInstances = new();

        private readonly List<IInitializable> _initializables = new();
        private readonly List<IUpdatable> _updatables = new();
        private readonly List<ILateUpdatable> _lateUpdatables = new();
        private readonly List<IDisposable> _disposables = new();

        // Reflection cache for performance optimization
        private static readonly Dictionary<Type, ConstructorInfo> _constructorCache = new();
        private static readonly Dictionary<Type, bool> _isInstantiableCache = new();
        private static readonly object _cacheLock = new object();

        private bool _initialized = false;
        private bool _disposed = false;

        // Set while RegisterInstance injects an instance, the innermost call first, so that a
        // failed injection can undo what it left in this scope.
        private RegistrationRollback _rollback;
        // How deep GetInstance calls are nested while a rollback is set: the call at depth d hands
        // what it resolves to the call at depth d - 1, or, at the depth where an injection began,
        // to the instance being injected.
        private int _resolveDepth;



        internal ContainerScope(Container container)
        {
            _container = container;
        }

        #region Lifecycle
        // Every lifecycle callback runs on its own: an exception is logged and the services
        // after the failing one still run, in this call and in the calls that follow.

        /// <summary>
        /// Run IInitializable once for tracked instances.
        /// </summary>
        public void Initialize()
        {
            if (_initialized || _disposed)
            {
                return;
            }

            _initialized = true;
            for (int i = 0; i < _initializables.Count; i++)
            {
                InitializeTracked(_initializables[i]);
            }
        }

        /// <summary>
        /// Call IUpdatable on tracked instances.
        /// </summary>
        public void Update()
        {
            if (_disposed)
            {
                return;
            }

            for (int i = 0; i < _updatables.Count; i++)
            {
                IUpdatable updatable = _updatables[i];
                if (updatable == null)
                {
                    continue;
                }

                try
                {
                    updatable.Update();
                }
                catch (Exception ex)
                {
                    LogLifecycleException(ex);
                }
            }
        }

        /// <summary>
        /// Call ILateUpdatable on tracked instances.
        /// </summary>
        public void LateUpdate()
        {
            if (_disposed)
            {
                return;
            }

            for (int i = 0; i < _lateUpdatables.Count; i++)
            {
                ILateUpdatable lateUpdatable = _lateUpdatables[i];
                if (lateUpdatable == null)
                {
                    continue;
                }

                try
                {
                    lateUpdatable.LateUpdate();
                }
                catch (Exception ex)
                {
                    LogLifecycleException(ex);
                }
            }
        }

        /// <summary>
        /// Dispose tracked instances in reverse order and clear scope cache.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            for (int i = _disposables.Count - 1; i >= 0; i--)
            {
                DisposeTracked(_disposables[i]);
            }

            _scopedInstances.Clear();
            _trackedInstances.Clear();
            _initializables.Clear();
            _updatables.Clear();
            _lateUpdatables.Clear();
            _disposables.Clear();
        }

        private void InitializeTracked(IInitializable initializable)
        {
            if (initializable == null)
            {
                return;
            }

            try
            {
                initializable.Initialize();
            }
            catch (Exception ex)
            {
                LogLifecycleException(ex);
            }
        }

        private void DisposeTracked(IDisposable disposable)
        {
            if (disposable == null)
            {
                return;
            }

            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                LogLifecycleException(ex);
            }
        }

        private void LogLifecycleException(Exception ex)
        {
            UnityEngine.Debug.LogException(ex, _container?.Owner);
        }
        #endregion

        #region Registration
        /// <summary>
        /// Register service with explicit implementation type.
        /// </summary>
        public void Register<TService, TImpl>(ServiceLifetime lifetime = ServiceLifetime.Singleton, string key = null)
            where TImpl : class, TService
            => _container.Register<TService, TImpl>(lifetime, key);

        /// <summary>
        /// Register a concrete type as itself and its interfaces.
        /// </summary>
        public void Register<TService>(ServiceLifetime lifetime = ServiceLifetime.Singleton, string key = null)
            where TService : class
            => _container.Register<TService>(lifetime, key);

        /// <summary>
        /// Register an already created instance, inject its members, and track its lifecycle.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When the injection throws, what this call left in the scope is undone before the
        /// exception reaches the caller:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Its registrations are removed, so the instance does not resolve
        /// through them, and the registration log (<c>[DI Registration]</c>) logs each removal
        /// as rolled back.</description></item>
        /// <item><description>A singleton or scoped service that the injection created and that
        /// received the instance from the container, directly or through another service the
        /// injection created, transient ones included, leaves the cache, so the next resolve
        /// creates it again. The scope stops tracking it and calls its
        /// <see cref="IDisposable.Dispose"/>, as its own <see cref="Dispose"/> would. A service the
        /// injection created that did not receive the instance stays.</description></item>
        /// <item><description>The lifecycle tracking this call started for the instance, during the
        /// injection included, stops. When the scope has already run the instance's
        /// <see cref="IInitializable.Initialize"/>, the instance gets
        /// <see cref="IDisposable.Dispose"/> to pair with it; one the scope did not initialize is
        /// not disposed, since the caller still owns it.</description></item>
        /// </list>
        /// <para>
        /// An earlier registration of the same instance stays, and so does the instance's
        /// tracking, since the instance still resolves through that registration.
        /// </para>
        /// <para>
        /// The scope knows only what it resolved itself. A service that holds the instance some
        /// other way, such as through a factory that closes over it, stays cached, and so does a
        /// service of another core that received the instance through
        /// <c>[Inject(typeof(TCore))]</c>, which that core's scope created. Creating an evicted
        /// service again runs its constructor again, side effects such as the registrations it
        /// makes included.
        /// </para>
        /// </remarks>
        public void RegisterInstance<TService>(TService instance, bool registerInterfaces = true, string key = null)
            where TService : class
        {
            int start = _container.DescriptorCount;
            bool wasTracked = instance != null && _trackedInstances.Contains(instance);
            _container.RegisterInstance(instance, registerInterfaces, key);
            int added = _container.DescriptorCount - start;

            // Apply member injection to instances registered from outside the container. The
            // instance is registered first, so a service that one of its members resolves may
            // depend on it, and resolving it there starts its lifecycle tracking. The caller gets
            // an exception for an instance whose injection failed, so its registration is taken
            // back rather than handed out, uninjected, by a later resolve, and so are the services
            // that received it meanwhile.
            RegistrationRollback rollback = new(instance, _resolveDepth, _rollback);
            _rollback = rollback;
            try
            {
                InjectInto(instance);
            }
            catch
            {
                _rollback = rollback.Outer;
                UndoRegistration(rollback, start, added, wasTracked);
                throw;
            }

            _rollback = rollback.Outer;

            // Track lifecycle for pre-created instances
            TrackLifecycle(instance, ServiceLifetime.Singleton);
        }

        // Undoes what a RegisterInstance call whose injection threw left in this scope; the
        // remarks on RegisterInstance describe the result. Only the descriptors the call added go.
        private void UndoRegistration(RegistrationRollback rollback, int start, int added, bool wasTracked)
        {
            _container.RemoveDescriptors(start, added);

            // The services that received the uninjected instance leave the caches before any of
            // them is disposed, so none resolves while it is, and they go newest first, as the
            // scope's own Dispose goes.
            List<CachedHolder> cached = rollback.Cached;
            for (int i = cached.Count - 1; i >= 0; i--)
            {
                CachedHolder holder = cached[i];
                if (holder.Descriptor.Lifetime == ServiceLifetime.Scoped)
                {
                    _scopedInstances.Remove(holder.Descriptor, holder.Instance);
                }
                else
                {
                    _container.RemoveSingleton(holder.Descriptor, holder.Instance);
                }
            }

            for (int i = cached.Count - 1; i >= 0; i--)
            {
                CachedHolder holder = cached[i];
                if (holder.StartedTracking && UntrackLifecycle(holder.Instance) && holder.Instance is IDisposable disposable)
                {
                    DisposeTracked(disposable);
                }
            }

            object instance = rollback.Instance;
            if (wasTracked || _container.HoldsInstance(instance))
            {
                return;
            }

            // A tracked instance has had its Initialize once the scope has initialized: at the
            // resolve that started the tracking, or in Initialize if the tracking came first.
            if (UntrackLifecycle(instance) && _initialized &&
                instance is IInitializable && instance is IDisposable instanceDisposable)
            {
                DisposeTracked(instanceDisposable);
            }
        }

        /// <summary>
        /// Register a factory method used to create the service.
        /// </summary>
        public void RegisterFactory<TService>(Func<ContainerScope, TService> factory, ServiceLifetime lifetime = ServiceLifetime.Singleton, string key = null)
            where TService : class
            => _container.RegisterFactory(factory, lifetime, key);
        #endregion

        #region Resolve
        /// <summary>
        /// Resolve a single service instance.
        /// </summary>
        public T Resolve<T>(string key = null) where T : class
        {
            return Resolve(typeof(T), key, true) as T;
        }

        /// <summary>
        /// Resolve all registered instances for a service.
        /// </summary>
        public IEnumerable<T> ResolveAll<T>(string key = null) where T : class
        {
            return ResolveAll(typeof(T), key).Cast<T>();
        }

        /// <summary>
        /// Try resolve a service without throwing.
        /// </summary>
        public bool TryResolve<T>(out T instance, string key = null) where T : class
        {
            object obj = Resolve(typeof(T), key, false);
            instance = obj as T;
            return instance != null;
        }

        private IEnumerable<object> ResolveAll(Type serviceType, string key)
        {
            IReadOnlyList<ServiceDescriptor> descriptors = _container.GetDescriptors(serviceType, key);
            if (descriptors.Count == 0)
            {
                return Array.Empty<object>();
            }

            List<object> results = new(descriptors.Count);
            HashSet<Type> chain = new();
            foreach (ServiceDescriptor descriptor in descriptors)
            {
                chain.Clear();
                object instance = GetInstance(descriptor, chain);
                if (instance != null)
                {
                    results.Add(instance);
                }
            }

            return results;
        }

        private object Resolve(Type serviceType, string key, bool throwIfMissing)
        {
            return ResolveInternal(serviceType, key, new HashSet<Type>(), throwIfMissing);
        }

        internal object Resolve(Type serviceType, string key)
        {
            return ResolveInternal(serviceType, key, new HashSet<Type>(), false);
        }

        private object ResolveInternal(Type serviceType, string key, HashSet<Type> chain, bool throwIfMissing)
        {
            if (serviceType == null || _disposed)
            {
                return null;
            }

            if (chain.Contains(serviceType))
            {
                string chainStr = string.Join(" -> ", chain.Select(t => t.Name));
                throw new InvalidOperationException($"Circular dependency detected: {chainStr} -> {serviceType.Name}. Check your service registrations to break the cycle.");
            }

            chain.Add(serviceType);

            if (serviceType == typeof(IObjectInjector) && string.IsNullOrEmpty(key))
            {
                chain.Remove(serviceType);
                return this;
            }

            if (serviceType.IsGenericType)
            {
                Type genericType = serviceType.GetGenericTypeDefinition();
                if (genericType == typeof(IEnumerable<>))
                {
                    Type elementType = serviceType.GetGenericArguments()[0];
                    object list = CreateEnumerable(elementType, key);
                    chain.Remove(serviceType);
                    return list;
                }

                if (genericType == typeof(Func<>))
                {
                    Type elementType = serviceType.GetGenericArguments()[0];
                    object factory = CreateFunc(elementType, key);
                    chain.Remove(serviceType);
                    return factory;
                }

                if (genericType == typeof(Lazy<>))
                {
                    Type elementType = serviceType.GetGenericArguments()[0];
                    object lazy = CreateLazy(elementType, key);
                    chain.Remove(serviceType);
                    return lazy;
                }
            }

            ServiceDescriptor descriptor = _container.GetDescriptor(serviceType, key);
            if (descriptor == null)
            {
                chain.Remove(serviceType);
                if (throwIfMissing)
                {
                    string keyInfo = string.IsNullOrEmpty(key) ? "" : $" with key '{key}'";
                    string coreInfo = _container.Owner != null ? $" in Core: {_container.Owner.GetType().Name}" : "";
                    throw new InvalidOperationException($"Service not registered: {serviceType.FullName}{keyInfo}{coreInfo}. Did you forget to register it in ConfigureContainer()?");
                }

                return null;
            }

            object instance = GetInstance(descriptor, chain);
            chain.Remove(serviceType);
            return instance;
        }

        private object GetInstance(ServiceDescriptor descriptor, HashSet<Type> chain)
        {
            if (_rollback == null)
            {
                return GetInstanceCore(descriptor, chain, out _, out _);
            }

            // RegisterInstance is injecting an instance: note which of the services this resolve
            // creates receive it, so that a failed injection can take them back.
            int depth = _resolveDepth++;
            object instance = null;
            bool cached = false;
            bool startedTracking = false;
            try
            {
                instance = GetInstanceCore(descriptor, chain, out cached, out startedTracking);
            }
            finally
            {
                _resolveDepth--;
                EndRecordedResolve(depth, descriptor, instance, cached, startedTracking);
            }

            NoteReceived(instance, depth - 1);
            return instance;
        }

        // cached: the instance was created by this call and put into the singleton or scoped
        // cache. startedTracking: this call started its lifecycle tracking.
        private object GetInstanceCore(ServiceDescriptor descriptor, HashSet<Type> chain, out bool cached, out bool startedTracking)
        {
            cached = false;
            startedTracking = false;
            object instance;

            if (descriptor.Instance != null)
            {
                instance = descriptor.Instance;
            }
            else if (descriptor.Lifetime == ServiceLifetime.Singleton)
            {
                bool created = false;
                instance = _container.GetOrCreateSingleton(descriptor, () =>
                {
                    created = true;
                    return CreateInstance(descriptor, chain);
                });
                cached = created;
            }
            else if (descriptor.Lifetime == ServiceLifetime.Scoped)
            {
                if (_scopedInstances.TryGet(descriptor, out instance))
                {
                    return instance;
                }

                instance = CreateInstance(descriptor, chain);
                _scopedInstances.Set(descriptor, instance);
                cached = true;
            }
            else // Transient
            {
                return CreateInstance(descriptor, chain);
            }

            startedTracking = TrackLifecycle(instance, descriptor.Lifetime);
            return instance;
        }

        // Ends the recorded resolve at depth: when what it ran received an instance being
        // registered, or a service that holds one, the instance it resolved holds it too.
        private void EndRecordedResolve(int depth, ServiceDescriptor descriptor, object instance, bool cached, bool startedTracking)
        {
            for (RegistrationRollback rollback = _rollback; rollback != null; rollback = rollback.Outer)
            {
                if (!rollback.HoldingDepths.Remove(depth) || instance == null)
                {
                    continue;
                }

                rollback.Holders.Add(instance);
                if (cached)
                {
                    rollback.Cached.Add(new CachedHolder(descriptor, instance, startedTracking));
                }
            }
        }

        // Notes that the resolve at depth received an object: the instance a RegisterInstance
        // call is injecting, or a service that holds it, makes that resolve a holder. A depth
        // below where the injection began is the instance itself receiving its members.
        private void NoteReceived(object received, int depth)
        {
            if (received == null)
            {
                return;
            }

            for (RegistrationRollback rollback = _rollback; rollback != null; rollback = rollback.Outer)
            {
                if (depth >= rollback.Depth &&
                    (ReferenceEquals(received, rollback.Instance) || rollback.Holders.Contains(received)))
                {
                    rollback.HoldingDepths.Add(depth);
                }
            }
        }

        private object CreateInstance(ServiceDescriptor descriptor, HashSet<Type> chain)
        {
            object instance;

            if (descriptor.Factory != null)
            {
                instance = descriptor.Factory(this);
            }
            else if (descriptor.ImplementationType == null)
            {
                return null;
            }
            else
            {
                instance = CreateInstance(descriptor.ImplementationType, chain);
            }

            // Registered services receive member injection before lifecycle tracking.
            InjectMembers(instance, chain);
            return instance;
        }

        private object CreateInstance(Type implementationType, HashSet<Type> chain)
        {
            // Check instantiability cache
            lock (_cacheLock)
            {
                if (_isInstantiableCache.TryGetValue(implementationType, out bool isInstantiable))
                {
                    if (!isInstantiable)
                    {
                        throw new InvalidOperationException($"Cannot instantiate abstract/interface type: {implementationType.FullName}");
                    }
                }
                else
                {
                    bool canInstantiate = !implementationType.IsAbstract && !implementationType.IsInterface;
                    _isInstantiableCache[implementationType] = canInstantiate;
                    if (!canInstantiate)
                    {
                        throw new InvalidOperationException($"Cannot instantiate abstract/interface type: {implementationType.FullName}");
                    }
                }

                // Check constructor cache
                if (_constructorCache.TryGetValue(implementationType, out ConstructorInfo cachedCtor))
                {
                    if (TryBuildParameters(cachedCtor, chain, out object[] args))
                    {
                        return cachedCtor.Invoke(args);
                    }
                }
            }

            // Find best constructor
            ConstructorInfo[] constructors = implementationType.GetConstructors();
            if (constructors.Length == 0)
            {
                return Activator.CreateInstance(implementationType);
            }

            foreach (ConstructorInfo ctor in constructors.OrderByDescending(t => t.GetParameters().Length))
            {
                if (TryBuildParameters(ctor, chain, out object[] args))
                {
                    lock (_cacheLock)
                    {
                        _constructorCache[implementationType] = ctor;
                    }
                    return ctor.Invoke(args);
                }
            }

            throw new InvalidOperationException($"No usable constructor found for type: {implementationType.FullName}. Ensure it has a public constructor with resolvable dependencies.");
        }

        private bool TryBuildParameters(ConstructorInfo ctor, HashSet<Type> chain, out object[] args)
        {
            ParameterInfo[] parameters = ctor.GetParameters();
            args = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo param = parameters[i];

                if (param.ParameterType == typeof(ContainerScope))
                {
                    args[i] = this;
                    continue;
                }

                if (param.ParameterType == typeof(Container))
                {
                    args[i] = _container;
                    continue;
                }

                object resolved = ResolveInternal(param.ParameterType, null, chain, false);
                if (resolved != null)
                {
                    args[i] = resolved;
                    continue;
                }

                if (param.HasDefaultValue)
                {
                    args[i] = param.DefaultValue;
                    continue;
                }

                return false;
            }

            return true;
        }

        private void InjectMembers(object instance, HashSet<Type> chain)
        {
            if (instance == null)
            {
                return;
            }

            Type type = instance.GetType();

            // A required [Inject] field that is readonly or static, or a required [Inject] property
            // that has no setter or is static, can never be filled. Throwing before anything
            // resolves, as for a required member whose service is missing, keeps it from staying
            // null without a word; a warning would leave the object running until the member is
            // first read, far from the cause.
            foreach (FieldInfo field in InjectableMembers.GetReadOnlyFieldArray(type))
            {
                if (field.GetCustomAttribute<InjectAttribute>().Required)
                {
                    throw new InvalidOperationException($"Inject failed. field is readonly: {type.FullName}.{field.Name}");
                }
            }

            foreach (PropertyInfo property in InjectableMembers.GetUnwritablePropertyArray(type))
            {
                if (property.GetCustomAttribute<InjectAttribute>().Required)
                {
                    throw new InvalidOperationException($"Inject failed. property has no setter: {type.FullName}.{property.Name}");
                }
            }

            // A static member belongs to the class, so injecting it would let each object overwrite
            // the value every other object of the class reads.
            foreach (FieldInfo field in InjectableMembers.GetStaticFieldArray(type))
            {
                if (field.GetCustomAttribute<InjectAttribute>().Required)
                {
                    throw new InvalidOperationException($"Inject failed. field is static: {type.FullName}.{field.Name}");
                }
            }

            foreach (PropertyInfo property in InjectableMembers.GetStaticPropertyArray(type))
            {
                if (property.GetCustomAttribute<InjectAttribute>().Required)
                {
                    throw new InvalidOperationException($"Inject failed. property is static: {type.FullName}.{property.Name}");
                }
            }

            FieldInfo[] injectableFields = InjectableMembers.GetFieldArray(type);
            foreach (FieldInfo field in injectableFields)
            {
                InjectAttribute attr = field.GetCustomAttribute<InjectAttribute>();

                object value = attr.CoreType == null
                    ? ResolveInternal(field.FieldType, attr.Key, chain, false)
                    : ResolveFromCore(attr.CoreType, field.FieldType, attr.Key);

                if (value == null)
                {
                    if (attr.Required)
                    {
                        throw new InvalidOperationException($"Inject failed. field: {type.FullName}.{field.Name}");
                    }
                    continue;
                }

                field.SetValue(instance, value);
            }

            PropertyInfo[] injectableProperties = InjectableMembers.GetPropertyArray(type);
            foreach (PropertyInfo property in injectableProperties)
            {
                InjectAttribute attr = property.GetCustomAttribute<InjectAttribute>();

                object value = attr.CoreType == null
                    ? ResolveInternal(property.PropertyType, attr.Key, chain, false)
                    : ResolveFromCore(attr.CoreType, property.PropertyType, attr.Key);

                if (value == null)
                {
                    if (attr.Required)
                    {
                        throw new InvalidOperationException($"Inject failed. property: {type.FullName}.{property.Name}");
                    }
                    continue;
                }

                property.SetValue(instance, value);
            }
        }

        internal void InjectInto(object instance)
        {
            InjectMembers(instance, new HashSet<Type>());
        }

        void IObjectInjector.Inject(object instance)
        {
            InjectInto(instance);
        }

        private object ResolveFromCore(Type coreType, Type serviceType, string key)
        {
            if (coreType == null || serviceType == null)
            {
                return null;
            }

            CoreBase owner = _container?.Owner;
            if (owner != null && owner.GetType() == coreType)
            {
                return owner.GetClassFromContainer(serviceType, key);
            }

            if (!typeof(CoreBase).IsAssignableFrom(coreType))
            {
                UnityEngine.Debug.LogError($"InjectFromCore failed. coreType is not CoreBase. type: {coreType.FullName}");
                return null;
            }

            return CoreRoot.Instance?.GetClass(coreType, serviceType, key);
        }

        // Returns whether this call started the tracking.
        private bool TrackLifecycle(object instance, ServiceLifetime lifetime)
        {
            if (instance == null || lifetime == ServiceLifetime.Transient)
            {
                return false;
            }

            if (!_trackedInstances.Add(instance))
            {
                return false;
            }

            if (instance is IInitializable initializable)
            {
                if (_initialized)
                {
                    InitializeTracked(initializable);
                }
                else
                {
                    _initializables.Add(initializable);
                }
            }

            if (instance is IUpdatable updatable)
            {
                _updatables.Add(updatable);
            }

            if (instance is ILateUpdatable lateUpdatable)
            {
                _lateUpdatables.Add(lateUpdatable);
            }

            if (instance is IDisposable disposable)
            {
                _disposables.Add(disposable);
            }

            return true;
        }

        // Undoes TrackLifecycle for an instance a failed registration takes back, and returns
        // whether the instance was tracked. It calls nothing on the instance: the caller decides
        // whether it gets Dispose.
        private bool UntrackLifecycle(object instance)
        {
            if (instance == null || !_trackedInstances.Remove(instance))
            {
                return false;
            }

            if (instance is IInitializable initializable)
            {
                _initializables.Remove(initializable);
            }

            if (instance is IUpdatable updatable)
            {
                _updatables.Remove(updatable);
            }

            if (instance is ILateUpdatable lateUpdatable)
            {
                _lateUpdatables.Remove(lateUpdatable);
            }

            if (instance is IDisposable disposable)
            {
                _disposables.Remove(disposable);
            }

            return true;
        }

        private object CreateEnumerable(Type elementType, string key)
        {
            IEnumerable<object> enumerable = ResolveAll(elementType, key);
            // Convert to list first to avoid multiple enumeration
            List<object> list = enumerable as List<object> ?? enumerable.ToList();
            Array array = Array.CreateInstance(elementType, list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                array.SetValue(list[i], i);
            }

            return array;
        }

        private object CreateFunc(Type elementType, string key)
        {
            MethodInfo method = typeof(ContainerScope).GetMethod(nameof(CreateFuncGeneric), BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo generic = method.MakeGenericMethod(elementType);
            return generic.Invoke(this, new object[] { key });
        }

        private Func<T> CreateFuncGeneric<T>(string key) where T : class
        {
            return () => Resolve<T>(key);
        }

        private object CreateLazy(Type elementType, string key)
        {
            MethodInfo method = typeof(ContainerScope).GetMethod(nameof(CreateLazyGeneric), BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo generic = method.MakeGenericMethod(elementType);
            return generic.Invoke(this, new object[] { key });
        }

        private Lazy<T> CreateLazyGeneric<T>(string key) where T : class
        {
            return new Lazy<T>(() => Resolve<T>(key));
        }
        #endregion

        // What one RegisterInstance call's injection leaves in the scope that a failure undoes.
        private sealed class RegistrationRollback
        {
            internal RegistrationRollback(object instance, int depth, RegistrationRollback outer)
            {
                Instance = instance;
                Depth = depth;
                Outer = outer;
            }

            // The instance being registered and injected.
            internal object Instance { get; }

            // The resolve depth at which the injection began.
            internal int Depth { get; }

            // The RegisterInstance call whose injection this call runs inside, if any.
            internal RegistrationRollback Outer { get; }

            // What the injection resolved that received the instance, directly or through
            // another of them. Compared by reference, as the instance is: a service that
            // overrides Equals must not make another object a holder.
            internal HashSet<object> Holders { get; } = new(ReferenceComparer.Instance);

            // The depths of the resolves still running that have received the instance or one
            // of the holders.
            internal HashSet<int> HoldingDepths { get; } = new();

            // The holders the injection created into the singleton or scoped cache, oldest first.
            internal List<CachedHolder> Cached { get; } = new();
        }

        private readonly struct CachedHolder
        {
            internal CachedHolder(ServiceDescriptor descriptor, object instance, bool startedTracking)
            {
                Descriptor = descriptor;
                Instance = instance;
                StartedTracking = startedTracking;
            }

            internal ServiceDescriptor Descriptor { get; }

            internal object Instance { get; }

            // Whether the resolve that created it started its lifecycle tracking.
            internal bool StartedTracking { get; }
        }

        // Object identity, which .NET Standard 2.1 has no comparer for.
        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceComparer Instance = new();

            bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);

            int IEqualityComparer<object>.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
