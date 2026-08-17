#if MU3LIBRARY_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;

namespace Mu3Library.UI.MVP
{
    public partial class MVPManager
    {
        /// <summary>
        /// Opens a window and completes when it finished its opening animation, or as soon as
        /// it left the manager because it was closed or destroyed on the way in.
        /// Returns what <see cref="Open{TPresenter}()"/> returns, null when the view resource
        /// is missing.
        /// </summary>
        public UniTask<IPresenter> OpenAsync<TPresenter>() where TPresenter : PresenterBase, new()
            => OpenAsync<TPresenter>(null, null, OutPanelSettings.Disabled, null);

        public UniTask<IPresenter> OpenAsync<TPresenter>(Arguments args) where TPresenter : PresenterBase, new()
            => OpenAsync<TPresenter>(null, args, OutPanelSettings.Disabled, null);

        public UniTask<IPresenter> OpenAsync<TPresenter>(Arguments args, OutPanelSettings settings) where TPresenter : PresenterBase, new()
            => OpenAsync<TPresenter>(null, args, settings, null);

        public async UniTask<IPresenter> OpenAsync<TPresenter>(IPresenter owner, Arguments args, OutPanelSettings settings, HostOptions hostOptions) where TPresenter : PresenterBase, new()
        {
            IPresenter presenter = Open<TPresenter>(owner, args, settings, hostOptions);
            if (presenter == null)
            {
                return null;
            }

            PresenterBase presenterBase = presenter as PresenterBase;
            await UniTask.WaitUntil(() => IsOpenSettled(presenterBase));

            return presenter;
        }

        /// <summary>
        /// Closes a window and completes when it left the manager, after its closing animation
        /// and unload ran. False when the window could not close, matching
        /// <see cref="Close(IPresenter, bool)"/>.
        /// </summary>
        public async UniTask<bool> CloseAsync(IPresenter presenter, bool forceClose = false)
        {
            if (!Close(presenter, forceClose))
            {
                return false;
            }

            PresenterBase presenterBase = presenter as PresenterBase;
            await UniTask.WaitUntil(() => FindPresenterEntry(presenterBase) == null);

            return true;
        }

        private bool IsOpenSettled(PresenterBase presenter)
        {
            PresenterEntry entry = FindPresenterEntry(presenter);
            if (entry == null)
            {
                // Released: the view was destroyed or the window already went through unload.
                return true;
            }

            return entry.Phase == PresenterPhase.Opened ||
                entry.Phase == PresenterPhase.Closing ||
                entry.Phase == PresenterPhase.Unloading;
        }
    }
}
#endif
