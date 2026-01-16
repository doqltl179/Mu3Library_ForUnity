using System;

namespace Mu3Library.Utility.Settings
{
    public interface IVolumeSettings
    {
        public float MasterVolume { get; set; }
        public float BgmVolume { get; set; }
        public float CalculatedBgmVolume { get; }
        public float SfxVolume { get; set; }
        public float CalculatedSfxVolume { get; }

        public event Action<float> OnMasterVolumeChanged;
        public event Action<float> OnBgmVolumeChanged;
        public event Action<float> OnSfxVolumeChanged;

        public void ResetVolumeAll();
        public void ResetMasterVolume();
        public void ResetBgmVolume();
        public void ResetSfxVolume();
    }
}
