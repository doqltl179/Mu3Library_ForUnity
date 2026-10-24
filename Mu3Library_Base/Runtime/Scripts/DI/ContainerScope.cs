using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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
                IDisposable disposable = _disposables[i];
                if (disposable == null)
                {
                    continue;
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
        public void RegisterInstance<TService>(TService instance, bool registerInterfaces = true, string key = null)
            where TService : class
        {
            _container.RegisterInstance(instance, registerInterfaces, key);

            // Apply member injection to instances registered from outside the container.
            InjectInto(instance);

            // Track lifecycle for pre-created instances
            TrackLifecycle(instance, ServiceLifetime.Singleton);
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
            object instance;

            if (descriptor.Instance != null)
            {
                instance = descriptor.Instance;
            }
            else if (descriptor.Lifetime == ServiceLifetime.Singleton)
            {
                instance = _container.GetOrCreateSingleton(descriptor, () => CreateInstance(descriptor, chain));
            }
            else if (descriptor.Lifetime == ServiceLifetime.Scoped)
            {
                if (_scopedInstances.TryGet(descriptor, out instance))
                {
                    return instance;
                }

                instance = CreateInstance(descriptor, chain);
                _scopedInstances.Set(descriptor, instance);
            }
            else // Transient
            {
                return CreateInstance(descriptor, chain);
            }

            TrackLifecycle(instance, descriptor.Lifetime);
            return instance;
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

        private void TrackLifecycle(object instance, ServiceLifetime lifetime)
        {
            if (instance == null || lifetime == ServiceLifetime.Transient)
            {
                return;
            }

            if (!_trackedInstances.Add(instance))
            {
                return;
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
    }
}
