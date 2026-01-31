using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mu3Library.DI
{
    /// <summary>
    /// Root registry for service descriptors and singleton cache.
    /// </summary>
    public class Container
    {
        private readonly CoreBase _owner;
        internal CoreBase Owner => _owner;

        private readonly List<ServiceDescriptor> _descriptors = new();
        private readonly Dictionary<ServiceKey, object> _singletons = new();
        // Cache singletons by implementation type to ensure standard interfaces share the same instance
        private readonly Dictionary<(Type, string), object> _implementationSingletons = new();

        private readonly HashSet<Type> _interfaceIgnoreTypes = new()
        {
            typeof(IInitializable),
            typeof(IDisposable),
            typeof(IUpdatable),
            typeof(ILateUpdatable),
        };



        public Container(CoreBase owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// Create a new resolution scope bound to this container.
        /// </summary>
        public ContainerScope CreateScope()
        {
            return new ContainerScope(this);
        }

        #region Registration
        /// <summary>
        /// Register service with explicit implementation type.
        /// </summary>
        public void Register<TService, TImpl>(ServiceLifetime lifetime = ServiceLifetime.Singleton, string key = null)
            where TImpl : class, TService
        {
            AddDescriptor(new ServiceDescriptor(typeof(TService), typeof(TImpl), lifetime, key));
        }

        /// <summary>
        /// Register a concrete type as itself and its interfaces.
        /// </summary>
        public void Register<TService>(ServiceLifetime lifetime = ServiceLifetime.Singleton, string key = null)
            where TService : class
        {
            RegisterSelfAndInterfaces(typeof(TService), lifetime, key);
        }

        /// <summary>
        /// Register an already created instance.
        /// </summary>
        public void RegisterInstance<TService>(TService instance, bool registerInterfaces = true, string key = null)
            where TService : class
        {
            if (instance == null)
            {
                Debug.LogError("RegisterInstance failed. instance is null.");
                return;
            }

            Type type = instance.GetType();
            if (registerInterfaces)
            {
                RegisterInterfaces(type, lifetime: ServiceLifetime.Singleton, key, instance);
            }

            AddDescriptor(new ServiceDescriptor(typeof(TService), instance, key));
        }

        /// <summary>
        /// Register a factory method used to create the service.
        /// </summary>
        public void RegisterFactory<TService>(Func<ContainerScope, TService> factory, ServiceLifetime lifetime = ServiceLifetime.Singleton, string key = null)
            where TService : class
        {
            if (factory == null)
            {
                Debug.LogError("RegisterFactory failed. factory is null.");
                return;
            }

            AddDescriptor(new ServiceDescriptor(typeof(TService), scope => factory(scope), lifetime, key));
        }

        public void RemoveInterfaceIgnoreType<T>()
            => RemoveInterfaceIgnoreType(typeof(T));

        public void RemoveInterfaceIgnoreType(Type type)
        {
            _interfaceIgnoreTypes.Remove(type);
        }

        public void AddInterfaceIgnoreType<T>()
            => AddInterfaceIgnoreType(typeof(T));

        public void AddInterfaceIgnoreType(Type type)
        {
            _interfaceIgnoreTypes.Add(type);
        }
        #endregion

        /// <summary>
        /// Get the last-registered descriptor for a service/key pair.
        /// </summary>
        internal ServiceDescriptor GetDescriptor(Type serviceType, string key)
        {
            if (serviceType == null)
            {
                return null;
            }

            for (int i = _descriptors.Count - 1; i >= 0; i--)
            {
                ServiceDescriptor descriptor = _descriptors[i];
                if (descriptor.ServiceType == serviceType && descriptor.Key == key)
                {
                    return descriptor;
                }
            }

            return null;
        }

        /// <summary>
        /// Get all descriptors for a service/key pair.
        /// </summary>
        internal IReadOnlyList<ServiceDescriptor> GetDescriptors(Type serviceType, string key)
        {
            if (serviceType == null)
            {
                return Array.Empty<ServiceDescriptor>();
            }

            return _descriptors.Where(d => d.ServiceType == serviceType && d.Key == key).ToArray();
        }

        /// <summary>
        /// Get or create a singleton instance for the given key.
        /// </summary>
        internal object GetOrCreateSingleton(ServiceKey key, Func<object> factory)
        {
            if (_singletons.TryGetValue(key, out object instance))
            {
                return instance;
            }

            // Try to find existing singleton by implementation type
            if (key.ImplementationType != null && 
                _implementationSingletons.TryGetValue((key.ImplementationType, key.Key), out instance))
            {
                _singletons[key] = instance;
                return instance;
            }

            instance = factory();
            _singletons[key] = instance;

            if (key.ImplementationType != null)
            {
                _implementationSingletons[(key.ImplementationType, key.Key)] = instance;
            }

            return instance;
        }

        private void RegisterSelfAndInterfaces(Type implementationType, ServiceLifetime lifetime, string key)
        {
            if (implementationType == null)
            {
                return;
            }

            AddDescriptor(new ServiceDescriptor(implementationType, implementationType, lifetime, key));
            RegisterInterfaces(implementationType, lifetime, key, null);
        }

        private void RegisterInterfaces(Type implementationType, ServiceLifetime lifetime, string key, object instance)
        {
            if (implementationType == null)
            {
                return;
            }

            foreach (Type iType in implementationType.GetInterfaces())
            {
                if (_interfaceIgnoreTypes.Contains(iType))
                {
                    continue;
                }

                if (instance == null)
                {
                    AddDescriptor(new ServiceDescriptor(iType, implementationType, lifetime, key));
                }
                else
                {
                    AddDescriptor(new ServiceDescriptor(iType, instance, key));
                }
            }
        }

        private void AddDescriptor(ServiceDescriptor descriptor)
        {
            if (descriptor == null || descriptor.ServiceType == null)
            {
                return;
            }

            if (_descriptors.Any(d =>
                d.ServiceType == descriptor.ServiceType &&
                d.ImplementationType == descriptor.ImplementationType &&
                d.Instance == descriptor.Instance &&
                d.Key == descriptor.Key))
            {
                return;
            }

            _descriptors.Add(descriptor);

            // Log registration
            string coreName = _owner != null ? _owner.GetType().Name : "Unknown Core";
            string implType = descriptor.ImplementationType != null ? descriptor.ImplementationType.Name : "Instance";
            string keyInfo = string.IsNullOrEmpty(descriptor.Key) ? "" : $" (Key: {descriptor.Key})";
            Debug.Log($"[DI Registration] Core: {coreName} | Service: {descriptor.ServiceType.Name} | Implementation: {implType} | Lifetime: {descriptor.Lifetime}{keyInfo}");
        }
    }
}
