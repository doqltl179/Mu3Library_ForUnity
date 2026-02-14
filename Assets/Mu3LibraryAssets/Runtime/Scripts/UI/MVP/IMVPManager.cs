using System.Collections.Generic;
using UnityEngine.EventSystems;
namespace Mu3Library.UI.MVP
{
    public partial interface IMVPManager
    {
        public EventSystem EventSystem { get; }
        public IEnumerable<string> SortingLayers { get; }

        public void ClearEventSystem();

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
