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
            PresenterEntry entry = FindPresenterEntry(presenterBase);
            if (entry == null)
            {
                return presenter;
            }

            await UniTask.WaitUntil(() => IsOpenSettled(presenterBase, entry));

            return presenter;
        }

        /// <summary>
        /// Closes a window and completes when the opening it closed left the manager, after its
        /// closing animation and unload ran, even if the same presenter has been opened again
        /// since. False when the window could not close, matching
        /// <see cref="Close(IPresenter, bool)"/>.
        /// </summary>
        public async UniTask<bool> CloseAsync(IPresenter presenter, bool forceClose = false)
        {
            PresenterBase presenterBase = presenter as PresenterBase;
            PresenterEntry entry = FindPresenterEntry(presenterBase);

            if (!Close(presenter, forceClose))
            {
                return false;
            }

            await UniTask.WaitUntil(() => !IsCurrentEntry(presenterBase, entry));

            return true;
        }

        /// <summary>
        /// Whether the opening an async call started is still the one the manager holds for the
        /// presenter. The entry, not the presenter, identifies that opening: presenters are pooled,
        /// so an <see cref="OnWindowUnloaded"/> handler can open the same instance again in the call
        /// that released it, and a check on the presenter alone would then wait for that later
        /// opening too.
        /// </summary>
        private bool IsCurrentEntry(PresenterBase presenter, PresenterEntry entry)
            => FindPresenterEntry(presenter) == entry;

        private bool IsOpenSettled(PresenterBase presenter, PresenterEntry entry)
        {
            if (!IsCurrentEntry(presenter, entry))
            {
                // Released: the view was destroyed or the window already went through unload,
                // whether or not the same presenter has been opened again since.
                return true;
            }

            return entry.Phase == PresenterPhase.Opened ||
                entry.Phase == PresenterPhase.Closing ||
                entry.Phase == PresenterPhase.Unloading;
        }
    }
}
#endif
