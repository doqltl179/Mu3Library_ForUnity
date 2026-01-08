#if MU3LIBRARY_UNITASK_SUPPORT

using Cysharp.Threading.Tasks;

namespace Mu3Library.DI
{
    public interface IInitializableAsync
    {
        public UniTask InitializeAsync();
    }
}

#endif