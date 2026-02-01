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
        // Index for fast descriptor lookup: (ServiceType, Key) -> List of indices
        private readonly Dictionary<(Type, string), List<int>> _descriptorIndex = new();

        private readonly Dictionary<ServiceKey, object> _singletons = new();
        // Cache singletons by implementation type to ensure all interfaces of same implementation share instance
        private readonly Dictionary<Type, object> _implementationSingletons = new();

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

            // Use index for fast lookup
            if (_descriptorIndex.TryGetValue((serviceType, key), out List<int> indices) && indices.Count > 0)
            {
                // Return the last registered (most recent)
                return _descriptors[indices[indices.Count - 1]];
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

            // Use index for fast lookup
            if (_descriptorIndex.TryGetValue((serviceType, key), out List<int> indices) && indices.Count > 0)
            {
                ServiceDescriptor[] result = new ServiceDescriptor[indices.Count];
                for (int i = 0; i < indices.Count; i++)
                {
                    result[i] = _descriptors[indices[i]];
                }
                return result;
            }

            return Array.Empty<ServiceDescriptor>();
        }

        /// <summary>
        /// Get or create a singleton instance for the given key.
        /// Ensures that all interfaces of the same implementation type share the same instance.
        /// </summary>
        internal object GetOrCreateSingleton(ServiceKey key, Func<object> factory)
        {
            // Fast path: check exact service/key combination first
            if (_singletons.TryGetValue(key, out object instance))
            {
                return instance;
            }

            // For default key (null/empty), check if implementation type already has an instance
            // This ensures all interfaces of same implementation share the same singleton
            bool hasImplementationType = key.ImplementationType != null;
            bool isDefaultKey = string.IsNullOrEmpty(key.Key);

            if (hasImplementationType && isDefaultKey)
            {
                if (_implementationSingletons.TryGetValue(key.ImplementationType, out instance))
                {
                    // Reuse existing instance and cache this service type mapping
                    _singletons[key] = instance;
                    return instance;
                }
            }

            // No existing instance found - create new one
            instance = factory();

            // Cache in both dictionaries for default key
            _singletons[key] = instance;
            if (hasImplementationType && isDefaultKey)
            {
                _implementationSingletons[key.ImplementationType] = instance;
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

            // Check for exact duplicate
            var indexKey = (descriptor.ServiceType, descriptor.Key);
            if (_descriptorIndex.TryGetValue(indexKey, out List<int> existingIndices))
            {
                // Check if this exact descriptor already exists
                foreach (int idx in existingIndices)
                {
                    ServiceDescriptor existing = _descriptors[idx];
                    if (existing.ImplementationType == descriptor.ImplementationType &&
                        existing.Instance == descriptor.Instance)
                    {
                        return; // Duplicate found, skip
                    }
                }
            }

            // Add descriptor and update index
            int newIndex = _descriptors.Count;
            _descriptors.Add(descriptor);

            if (existingIndices == null)
            {
                _descriptorIndex[indexKey] = new List<int> { newIndex };
            }
            else
            {
                existingIndices.Add(newIndex);
            }

            // Log registration (optional, can be disabled for production)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string coreName = _owner != null ? _owner.GetType().Name : "Unknown Core";
            string implType = descriptor.ImplementationType != null ? descriptor.ImplementationType.Name : "Instance";
            string keyInfo = string.IsNullOrEmpty(descriptor.Key) ? "" : $" (Key: {descriptor.Key})";
            Debug.Log($"[DI Registration] Core: {coreName} | Service: {descriptor.ServiceType.Name} | Implementation: {implType} | Lifetime: {descriptor.Lifetime}{keyInfo}");
#endif
        }
    }
}
