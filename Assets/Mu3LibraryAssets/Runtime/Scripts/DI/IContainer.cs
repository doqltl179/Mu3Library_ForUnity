using System;

#if MU3LIBRARY_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif

namespace Mu3Library.DI
{
    public interface IContainer
    {
        public T Get<T>() where T : class;

#if MU3LIBRARY_UNITASK_SUPPORT
        public UniTask DisposeAsync();
        public UniTask InitializaAsync();
#endif
        public void LateUpdate();
        public void Update();
        public void Dispose();
        public void Initialze();

        public void RemoveInterfaceIgnoreType<T>();
        public void RemoveInterfaceIgnoreType(Type type);
        public void AddInterfaceIgnoreType<T>();
        public void AddInterfaceIgnoreType(Type type);

        public void Register<T>() where T : class, new();
    }
}
