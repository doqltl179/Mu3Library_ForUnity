using System;
using System.Collections.Generic;

namespace Mu3Library.DI
{
    /// <summary>
    /// The instances one lifetime created, keyed by the registration that produced them.
    /// Type registrations that share an implementation type and key are one registration, so
    /// they share their instance whatever service type resolves it; every factory
    /// registration is its own. The container keeps one cache for its singletons and each
    /// scope one for its scoped instances, which makes the lifetime the third part of that
    /// identity.
    /// </summary>
    internal sealed class RegistrationInstanceCache
    {
        private readonly Dictionary<(Type, string), object> _typeInstances = new();
        private readonly Dictionary<ServiceDescriptor, object> _factoryInstances = new();



        internal bool TryGet(ServiceDescriptor descriptor, out object instance)
        {
            return descriptor.Factory != null
                ? _factoryInstances.TryGetValue(descriptor, out instance)
                : _typeInstances.TryGetValue(GetTypeKey(descriptor), out instance);
        }

        internal void Set(ServiceDescriptor descriptor, object instance)
        {
            if (descriptor.Factory != null)
            {
                _factoryInstances[descriptor] = instance;
            }
            else
            {
                _typeInstances[GetTypeKey(descriptor)] = instance;
            }
        }

        internal void CollectInstances(HashSet<object> instances)
        {
            foreach (object instance in _typeInstances.Values)
            {
                if (instance != null)
                {
                    instances.Add(instance);
                }
            }

            foreach (object instance in _factoryInstances.Values)
            {
                if (instance != null)
                {
                    instances.Add(instance);
                }
            }
        }

        internal void Clear()
        {
            _typeInstances.Clear();
            _factoryInstances.Clear();
        }

        private static (Type, string) GetTypeKey(ServiceDescriptor descriptor)
        {
            // An empty key is the default key, as it was when singletons were shared by
            // implementation type.
            string key = string.IsNullOrEmpty(descriptor.Key) ? null : descriptor.Key;
            return (descriptor.ImplementationType, key);
        }
    }
}
