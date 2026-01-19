using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mu3Library.DI
{
    public class Container
    {
        private readonly List<ServiceDescriptor> _descriptors = new();
        private readonly Dictionary<ServiceKey, object> _singletons = new();

        private readonly HashSet<Type> _interfaceIgnoreTypes = new()
        {
            typeof(IInitializable),
            typeof(IDisposable),
            typeof(IUpdatable),
            typeof(ILateUpdatable),
        };

        public ContainerScope CreateScope()
        {
            return new ContainerScope(this);
        }

        #region Registration
        public void Register<TService, TImpl>(ServiceLifetime lifetime = ServiceLifetime.Singleton, string key = null)
            where TImpl : class, TService
        {
            AddDescriptor(new ServiceDescriptor(typeof(TService), typeof(TImpl), lifetime, key));
        }

        public void Register<TService>(ServiceLifetime lifetime = ServiceLifetime.Singleton, string key = null)
            where TService : class
        {
            RegisterSelfAndInterfaces(typeof(TService), lifetime, key);
        }

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

        internal IReadOnlyList<ServiceDescriptor> GetDescriptors(Type serviceType, string key)
        {
            if (serviceType == null)
            {
                return Array.Empty<ServiceDescriptor>();
            }

            return _descriptors.Where(d => d.ServiceType == serviceType && d.Key == key).ToArray();
        }

        internal object GetOrCreateSingleton(ServiceKey key, Func<object> factory)
        {
            if (_singletons.TryGetValue(key, out object instance))
            {
                return instance;
            }

            instance = factory();
            _singletons[key] = instance;
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
        }
    }
}
