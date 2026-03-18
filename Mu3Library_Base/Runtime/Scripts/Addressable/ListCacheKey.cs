#if MU3LIBRARY_ADDRESSABLES_SUPPORT
using System;

namespace Mu3Library.Addressable
{
    internal readonly struct ListCacheKey : IEquatable<ListCacheKey>
    {
        private readonly object _key;
        private readonly Type _type;



        public ListCacheKey(object key, Type type)
        {
            _key = key;
            _type = type;
        }

        public bool Equals(ListCacheKey other)
        {
            return Equals(_key, other._key) && _type == other._type;
        }

        public static ListCacheKey Create(object key, Type type)
        {
            return new ListCacheKey(key, type);
        }
    }
}
#endif
