using System;
using System.Collections.Generic;
namespace Mu3Library.UI.MVP
{
    public partial interface IMVPManager
    {
        public event Action<IPresenter> OnWindowLoaded;
        public event Action<IPresenter> OnWindowOpened;
        public event Action<IPresenter> OnWindowClosed;
        public event Action<IPresenter> OnWindowUnloaded;

        public void CloseAllWithoutDefault(bool forceClose = false);
        public void CloseAll(bool forceClose = false);
        public void CloseAll(IEnumerable<string> layerNames, bool forceClose = false);
        public void CloseFocused(bool forceClose = false);
        public bool Close(IPresenter presenter, bool forceClose = false);

        public IPresenter Open<TPresenter>() where TPresenter : PresenterBase, new();
        public IPresenter Open<TPresenter>(Arguments args) where TPresenter : PresenterBase, new();
        public IPresenter Open<TPresenter>(OutPanelSettings settings) where TPresenter : PresenterBase, new();
        public IPresenter Open<TPresenter>(Arguments args, OutPanelSettings settings) where TPresenter : PresenterBase, new();
    }
}
