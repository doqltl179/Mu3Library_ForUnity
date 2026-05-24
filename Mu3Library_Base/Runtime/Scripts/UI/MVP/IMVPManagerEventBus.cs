using System;

namespace Mu3Library.UI.MVP
{
    public interface IMVPManagerEventBus
    {
        public event Action<IPresenter> OnWindowLoaded;
        public event Action<IPresenter> OnWindowOpened;
        public event Action<IPresenter> OnWindowClosed;
        public event Action<IPresenter> OnWindowUnloaded;

        public uint SubscribeOnWindowLoadedOnce(Action<IPresenter> callback);
        public uint SubscribeOnWindowLoadedOnce(Action<IPresenter> callback, Action onDisposed);
        public uint SubscribeOnWindowOpenedOnce(Action<IPresenter> callback);
        public uint SubscribeOnWindowOpenedOnce(Action<IPresenter> callback, Action onDisposed);
        public uint SubscribeOnWindowClosedOnce(Action<IPresenter> callback);
        public uint SubscribeOnWindowClosedOnce(Action<IPresenter> callback, Action onDisposed);
        public uint SubscribeOnWindowUnloadedOnce(Action<IPresenter> callback);
        public uint SubscribeOnWindowUnloadedOnce(Action<IPresenter> callback, Action onDisposed);
    }
}
