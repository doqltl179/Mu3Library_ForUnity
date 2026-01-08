#if MU3LIBRARY_UNITASK_SUPPORT

using Cysharp.Threading.Tasks;

namespace Mu3Library.DI
{
    public interface IDisposableAsync
    {
        public UniTask DisposeAsync();
    }
}

#endif