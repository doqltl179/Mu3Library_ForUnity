using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Mu3Library.DI
{
    /// <summary>
    /// Resolution scope that owns scoped instances and lifecycle callbacks.
    /// </summary>
    public sealed class ContainerScope : IDisposable
    {
        private readonly Container _container;
        private readonly Dictionary<ServiceKey, object> _scopedInstances = new();
        private readonly HashSet<object> _trackedInstances = new();

        private readonly List<IInitializable> _initializables = new();
        private readonly List<IUpdatable> _updatables = new();
        private readonly List<ILateUpdatable> _lateUpdatables = new();
        private readonly List<IDisposable> _disposables = new();

        // Reflection cache for performance optimization
        private static readonly Dictionary<Type, FieldInfo[]> _injectableFieldsCache = new();
        private static readonly Dictionary<Type, PropertyInfo[]> _injectablePropertiesCache = new();
        private static readonly object _cacheLock = new object();

        private bool _initialized = false;
        private bool _disposed = false;



        internal ContainerScope(Container container)
        {
            _container = container;
        }

        #region Lifecycle
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
                _initializables[i]?.Initialize();
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
                _updatables[i]?.Update();
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
                _lateUpdatables[i]?.LateUpdate();
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
                _disposables[i]?.Dispose();
            }

            _scopedInstances.Clear();
            _trackedInstances.Clear();
            _initializables.Clear();
            _updatables.Clear();
            _lateUpdatables.Clear();
            _disposables.Clear();
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
        /// Register an already created instance.
        /// </summary>
        public void RegisterInstance<TService>(TService instance, bool registerInterfaces = true, string key = null)
            where TService : class
            => _container.RegisterInstance(instance, registerInterfaces, key);

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
            foreach (ServiceDescriptor descriptor in descriptors)
            {
                object instance = GetInstance(descriptor, serviceType, key, new HashSet<Type>());
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
                throw new InvalidOperationException($"Circular dependency detected. type: {serviceType.FullName}");
            }

            chain.Add(serviceType);

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
                    throw new InvalidOperationException($"Service not registered. type: {serviceType.FullName}");
                }

                return null;
            }

            object instance = GetInstance(descriptor, serviceType, key, chain);
            chain.Remove(serviceType);
            return instance;
        }

        private object GetInstance(ServiceDescriptor descriptor, Type serviceType, string key, HashSet<Type> chain)
        {
            if (descriptor.Instance != null)
            {
                if (!_trackedInstances.Contains(descriptor.Instance))
                {
                    InjectMembers(descriptor.Instance, chain);
                }
                TrackLifecycle(descriptor.Instance, ServiceLifetime.Singleton);
                return descriptor.Instance;
            }

            if (descriptor.Lifetime == ServiceLifetime.Singleton)
            {
                ServiceKey skey = new(serviceType, descriptor.ImplementationType ?? serviceType, key);
                object instance = _container.GetOrCreateSingleton(skey, () => CreateInstance(descriptor, chain));
                TrackLifecycle(instance, ServiceLifetime.Singleton);
                return instance;
            }

            if (descriptor.Lifetime == ServiceLifetime.Scoped)
            {
                ServiceKey skey = new(serviceType, descriptor.ImplementationType ?? serviceType, key);
                if (_scopedInstances.TryGetValue(skey, out object scoped))
                {
                    TrackLifecycle(scoped, ServiceLifetime.Scoped);
                    return scoped;
                }

                object instance = CreateInstance(descriptor, chain);
                _scopedInstances[skey] = instance;
                TrackLifecycle(instance, ServiceLifetime.Scoped);
                return instance;
            }

            return CreateInstance(descriptor, chain);
        }

        private object CreateInstance(ServiceDescriptor descriptor, HashSet<Type> chain)
        {
            if (descriptor.Factory != null)
            {
                object instance = descriptor.Factory(this);
                InjectMembers(instance, chain);
                return instance;
            }

            if (descriptor.ImplementationType == null)
            {
                return null;
            }

            return CreateInstance(descriptor.ImplementationType, chain);
        }

        private object CreateInstance(Type implementationType, HashSet<Type> chain)
        {
            if (implementationType.IsAbstract || implementationType.IsInterface)
            {
                throw new InvalidOperationException($"Cannot instantiate abstract/interface type: {implementationType.FullName}");
            }

            ConstructorInfo[] constructors = implementationType.GetConstructors();
            if (constructors.Length == 0)
            {
                object instance = Activator.CreateInstance(implementationType);
                InjectMembers(instance, chain);
                return instance;
            }

            foreach (ConstructorInfo ctor in constructors.OrderByDescending(t => t.GetParameters().Length))
            {
                if (TryBuildParameters(ctor, chain, out object[] args))
                {
                    object instance = ctor.Invoke(args);
                    InjectMembers(instance, chain);
                    return instance;
                }
            }

            throw new InvalidOperationException($"No usable constructor found. type: {implementationType.FullName}");
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

            // Get or cache injectable fields
            FieldInfo[] injectableFields = GetInjectableFields(type);
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

            // Get or cache injectable properties
            PropertyInfo[] injectableProperties = GetInjectableProperties(type);
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

        private static FieldInfo[] GetInjectableFields(Type type)
        {
            lock (_cacheLock)
            {
                if (_injectableFieldsCache.TryGetValue(type, out FieldInfo[] cached))
                {
                    return cached;
                }

                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                FieldInfo[] allFields = type.GetFields(flags);
                List<FieldInfo> injectableFields = new List<FieldInfo>();

                foreach (FieldInfo field in allFields)
                {
                    if (field.GetCustomAttribute<InjectAttribute>() != null &&
                        !field.IsInitOnly &&
                        !field.IsStatic)
                    {
                        injectableFields.Add(field);
                    }
                }

                FieldInfo[] result = injectableFields.ToArray();
                _injectableFieldsCache[type] = result;
                return result;
            }
        }

        private static PropertyInfo[] GetInjectableProperties(Type type)
        {
            lock (_cacheLock)
            {
                if (_injectablePropertiesCache.TryGetValue(type, out PropertyInfo[] cached))
                {
                    return cached;
                }

                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                PropertyInfo[] allProperties = type.GetProperties(flags);
                List<PropertyInfo> injectableProperties = new List<PropertyInfo>();

                foreach (PropertyInfo property in allProperties)
                {
                    if (property.GetCustomAttribute<InjectAttribute>() != null &&
                        property.CanWrite &&
                        property.GetIndexParameters().Length == 0)
                    {
                        injectableProperties.Add(property);
                    }
                }

                PropertyInfo[] result = injectableProperties.ToArray();
                _injectablePropertiesCache[type] = result;
                return result;
            }
        }

        internal void InjectInto(object instance)
        {
            InjectMembers(instance, new HashSet<Type>());
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

            return CoreRoot.Instance.GetClass(coreType, serviceType, key);
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
                    initializable.Initialize();
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
            IEnumerable<object> list = ResolveAll(elementType, key);
            Array array = Array.CreateInstance(elementType, list.Count());
            int index = 0;
            foreach (object item in list)
            {
                array.SetValue(item, index++);
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
