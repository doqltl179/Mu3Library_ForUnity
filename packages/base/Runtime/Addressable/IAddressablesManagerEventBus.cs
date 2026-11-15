#if MU3LIBRARY_ADDRESSABLES_SUPPORT
using System;
using Mu3Library.Foundation.Event;

namespace Mu3Library.Addressable
{
    public interface IAddressablesManagerEventBus
    {
        public event Action OnInitialized;
        public event Action<bool, string> OnInitializeResult;
        public event Action<float> OnInitializeProgress;
        public event Action<float> OnDownloadProgress;

        public ISubscriptionInfo SubscribeOnInitializedOnce(Action callback);
        public ISubscriptionInfo SubscribeOnInitializedOnce(Action callback, Action onDisposed);
        public ISubscriptionInfo SubscribeOnInitializeResultOnce(Action<bool, string> callback);
        public ISubscriptionInfo SubscribeOnInitializeResultOnce(Action<bool, string> callback, Action onDisposed);
    }
}
#endif
