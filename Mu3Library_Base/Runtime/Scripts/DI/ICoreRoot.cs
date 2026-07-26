using System;

namespace Mu3Library.DI
{
    public interface ICoreRoot
    {
        public T GetClass<TCore, T>()
            where TCore : CoreBase
            where T : class;

        public object GetClass(Type coreType, Type serviceType, string key);

        public bool HasCore<T>() where T : CoreBase;
    }
}