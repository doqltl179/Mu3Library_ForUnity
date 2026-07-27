#if MU3LIBRARY_LOCALIZATION_SUPPORT
using System;
using Mu3Library.Foundation.Event;

namespace Mu3Library.Localization
{
    public interface ILocalizationManagerEventBus
    {
        public event Action OnInitialized;
        public event Action<bool, string> OnInitializeResult;
        public event Action<float> OnInitializeProgress;

        public ISubscriptionInfo SubscribeOnInitializedOnce(Action callback);
        public ISubscriptionInfo SubscribeOnInitializedOnce(Action callback, Action onDisposed);
        public ISubscriptionInfo SubscribeOnInitializeResultOnce(Action<bool, string> callback);
        public ISubscriptionInfo SubscribeOnInitializeResultOnce(Action<bool, string> callback, Action onDisposed);
    }
}
#endif
