#if MU3LIBRARY_ADDRESSABLES_SUPPORT
using System;

namespace Mu3Library.Addressable
{
    public interface IAddressablesManagerEventBus
    {
        public event Action OnInitialized;
        public event Action<bool, string> OnInitializeResult;
        public event Action<float> OnInitializeProgress;
        public event Action<float> OnDownloadProgress;

        public uint SubscribeOnInitializedOnce(Action callback);
        public uint SubscribeOnInitializedOnce(Action callback, Action onDisposed);
        public uint SubscribeOnInitializeResultOnce(Action<bool, string> callback);
        public uint SubscribeOnInitializeResultOnce(Action<bool, string> callback, Action onDisposed);
    }
}
#endif
