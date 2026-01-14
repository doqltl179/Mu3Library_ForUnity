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
        public UniTask InitializeAsync();
#endif
        public void LateUpdate();
        public void Update();
        public void Dispose();
        public void Initialize();

        public void RemoveInterfaceIgnoreType<T>();
        public void RemoveInterfaceIgnoreType(System.Type type);
        public void AddInterfaceIgnoreType<T>();
        public void AddInterfaceIgnoreType(System.Type type);

        public void Register<T>() where T : class, new();
    }
}
