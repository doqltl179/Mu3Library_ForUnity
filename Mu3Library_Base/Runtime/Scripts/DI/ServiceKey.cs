using System;

namespace Mu3Library.DI
{
    /// <summary>
    /// Cache key for singleton/scoped instances.
    /// </summary>
    public readonly struct ServiceKey : IEquatable<ServiceKey>
    {
        public readonly Type ServiceType;
        public readonly Type ImplementationType;
        public readonly string Key;

        public ServiceKey(Type serviceType, Type implementationType, string key)
        {
            ServiceType = serviceType;
            ImplementationType = implementationType;
            Key = key;
        }

        public bool Equals(ServiceKey other)
        {
            return ServiceType == other.ServiceType &&
                ImplementationType == other.ImplementationType &&
                Key == other.Key;
        }

        public override bool Equals(object obj)
        {
            return obj is ServiceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ServiceType, ImplementationType, Key);
        }
    }
}
