using System;

namespace Mu3Library.DI
{
    /// <summary>
    /// Defines how a service is created and its lifetime.
    /// </summary>
    public sealed class ServiceDescriptor
    {
        public Type ServiceType { get; }
        public Type ImplementationType { get; }
        public ServiceLifetime Lifetime { get; }
        public Func<ContainerScope, object> Factory { get; }
        public object Instance { get; }
        public string Key { get; }

        public ServiceDescriptor(Type serviceType, Type implementationType, ServiceLifetime lifetime, string key = null)
        {
            ServiceType = serviceType;
            ImplementationType = implementationType;
            Lifetime = lifetime;
            Key = key;
        }

        public ServiceDescriptor(Type serviceType, object instance, string key = null)
        {
            ServiceType = serviceType;
            ImplementationType = instance != null ? instance.GetType() : null;
            Instance = instance;
            Lifetime = ServiceLifetime.Singleton;
            Key = key;
        }

        public ServiceDescriptor(Type serviceType, Func<ContainerScope, object> factory, ServiceLifetime lifetime, string key = null)
        {
            ServiceType = serviceType;
            Factory = factory;
            Lifetime = lifetime;
            Key = key;
        }
    }
}
