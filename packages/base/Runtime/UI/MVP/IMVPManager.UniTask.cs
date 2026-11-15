#if MU3LIBRARY_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;

namespace Mu3Library.UI.MVP
{
    public partial interface IMVPManager
    {
        public UniTask<IPresenter> OpenAsync<TPresenter>() where TPresenter : PresenterBase, new();
        public UniTask<IPresenter> OpenAsync<TPresenter>(Arguments args) where TPresenter : PresenterBase, new();
        public UniTask<IPresenter> OpenAsync<TPresenter>(Arguments args, OutPanelSettings settings) where TPresenter : PresenterBase, new();
        public UniTask<IPresenter> OpenAsync<TPresenter>(IPresenter owner, Arguments args, OutPanelSettings settings, HostOptions hostOptions) where TPresenter : PresenterBase, new();

        public UniTask<bool> CloseAsync(IPresenter presenter, bool forceClose = false);
    }
}
#endif
