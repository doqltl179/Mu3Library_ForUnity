using System;

namespace Mu3Library.UI.MVP
{
    public interface IMVPManagerEventBus
    {
        public event Action<IPresenter> OnWindowLoaded;
        public event Action<IPresenter> OnWindowOpened;
        public event Action<IPresenter> OnWindowClosed;
        public event Action<IPresenter> OnWindowUnloaded;
    }
}
