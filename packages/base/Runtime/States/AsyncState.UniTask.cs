#if MU3LIBRARY_UNITASK_SUPPORT
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Mu3Library.States
{
    /// <summary>An <see cref="IAsyncState"/> that does nothing until it overrides what it needs.</summary>
    public abstract class AsyncState : IAsyncState
    {
        public virtual UniTask EnterAsync(CancellationToken cancellationToken) => UniTask.CompletedTask;

        public virtual void Update()
        {
        }

        public virtual UniTask ExitAsync(CancellationToken cancellationToken) => UniTask.CompletedTask;

        IEnumerator IState.Enter() => null;
        IEnumerator IState.Exit() => null;
    }
}
#endif
