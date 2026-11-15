using System;

namespace Mu3Library.DI
{
    /// <summary>
    /// A service, implementation, and key triple.
    /// </summary>
    /// <remarks>
    /// The container no longer keys its singleton and scoped instances by this triple: the
    /// service type is not part of a registration's identity, so one registration answers every
    /// service type with the same instance. The type stays for code that already uses it.
    /// </remarks>
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
