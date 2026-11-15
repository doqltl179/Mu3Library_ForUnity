using System;
using Mu3Library.Foundation.Event;

namespace Mu3Library.UI.MVP
{
    public interface IMVPManagerEventBus
    {
        public event Action<IPresenter> OnWindowLoaded;
        public event Action<IPresenter> OnWindowOpened;
        public event Action<IPresenter> OnWindowClosed;
        public event Action<IPresenter> OnWindowUnloaded;

        public ISubscriptionInfo SubscribeOnWindowLoadedOnce(Action<IPresenter> callback);
        public ISubscriptionInfo SubscribeOnWindowLoadedOnce(Action<IPresenter> callback, Action onDisposed);
        public ISubscriptionInfo SubscribeOnWindowOpenedOnce(Action<IPresenter> callback);
        public ISubscriptionInfo SubscribeOnWindowOpenedOnce(Action<IPresenter> callback, Action onDisposed);
        public ISubscriptionInfo SubscribeOnWindowClosedOnce(Action<IPresenter> callback);
        public ISubscriptionInfo SubscribeOnWindowClosedOnce(Action<IPresenter> callback, Action onDisposed);
        public ISubscriptionInfo SubscribeOnWindowUnloadedOnce(Action<IPresenter> callback);
        public ISubscriptionInfo SubscribeOnWindowUnloadedOnce(Action<IPresenter> callback, Action onDisposed);
    }
}
