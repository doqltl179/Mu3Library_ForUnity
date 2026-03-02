using System;

namespace Mu3Library.Audio
{
    public interface IAudioManagerEventBus
    {
        public event Action<float> OnMasterVolumeChanged;
        public event Action<float> OnBgmVolumeChanged;
        public event Action<float> OnSfxVolumeChanged;
        public event Action<float> OnEnvironmentVolumeChanged;
    }
}
