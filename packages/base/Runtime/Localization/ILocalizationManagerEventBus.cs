#if MU3LIBRARY_LOCALIZATION_SUPPORT
using System;
using Mu3Library.Foundation.Event;
using UnityEngine.Localization;

namespace Mu3Library.Localization
{
    public interface ILocalizationManagerEventBus
    {
        public event Action OnInitialized;
        public event Action<bool, string> OnInitializeResult;
        public event Action<float> OnInitializeProgress;
        /// <summary>
        /// Raised for every selected locale change, including one made outside this manager.
        /// <br/> The manager keeps the subscription on the Localization package for its whole life
        /// <br/> and takes it back off in <see cref="IDisposable.Dispose"/>.
        /// </summary>
        public event Action<Locale> OnLocaleChanged;

        public ISubscriptionInfo SubscribeOnInitializedOnce(Action callback);
        public ISubscriptionInfo SubscribeOnInitializedOnce(Action callback, Action onDisposed);
        public ISubscriptionInfo SubscribeOnInitializeResultOnce(Action<bool, string> callback);
        public ISubscriptionInfo SubscribeOnInitializeResultOnce(Action<bool, string> callback, Action onDisposed);
    }
}
#endif
