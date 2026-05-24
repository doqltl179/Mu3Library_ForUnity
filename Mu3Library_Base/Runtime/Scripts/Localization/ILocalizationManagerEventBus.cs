#if MU3LIBRARY_LOCALIZATION_SUPPORT
using System;

namespace Mu3Library.Localization
{
    public interface ILocalizationManagerEventBus
    {
        public event Action OnInitialized;
        public event Action<bool, string> OnInitializeResult;
        public event Action<float> OnInitializeProgress;

        public uint SubscribeOnInitializedOnce(Action callback);
        public uint SubscribeOnInitializedOnce(Action callback, Action onDisposed);
        public uint SubscribeOnInitializeResultOnce(Action<bool, string> callback);
        public uint SubscribeOnInitializeResultOnce(Action<bool, string> callback, Action onDisposed);
    }
}
#endif
