using System;

namespace Mu3Library.DI
{
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
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (ServiceType != null ? ServiceType.GetHashCode() : 0);
                hash = hash * 31 + (ImplementationType != null ? ImplementationType.GetHashCode() : 0);
                hash = hash * 31 + (Key != null ? Key.GetHashCode() : 0);
                return hash;
            }
        }
    }
}
