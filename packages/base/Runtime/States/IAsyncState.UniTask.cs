#if MU3LIBRARY_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Mu3Library.States
{
    /// <summary>
    /// A state that enters and exits with UniTask: the machine awaits <see cref="EnterAsync"/> and
    /// <see cref="ExitAsync"/> instead of stepping <see cref="IState.Enter"/> and
    /// <see cref="IState.Exit"/>, which it then never calls. The token is cancelled when the
    /// machine is disposed. Derive from <see cref="AsyncState"/> to override only what the state needs.
    /// </summary>
    public interface IAsyncState : IState
    {
        public UniTask EnterAsync(CancellationToken cancellationToken);
        public UniTask ExitAsync(CancellationToken cancellationToken);
    }
}
#endif
