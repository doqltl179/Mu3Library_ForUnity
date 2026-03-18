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
    }
}
#endif
