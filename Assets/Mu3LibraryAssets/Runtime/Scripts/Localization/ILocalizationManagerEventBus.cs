#if MU3LIBRARY_LOCALIZATION_SUPPORT
using System;

namespace Mu3Library.Localization
{
    public interface ILocalizationManagerEventBus
    {
        public event Action OnInitialized;
        public event Action<bool, string> OnInitializeResult;
        public event Action<float> OnInitializeProgress;
    }
}
#endif
