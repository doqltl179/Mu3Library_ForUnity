using System;
using Mu3Library.Utility.Settings;

namespace Mu3Library.Audio
{
    public interface IAudioVolumeSettings : IVolumeSettings
    {
        public event Action<float> OnMasterVolumeChanged;
        public event Action<float> OnBgmVolumeChanged;
        public event Action<float> OnSfxVolumeChanged;
    }
}
