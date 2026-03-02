namespace Mu3Library.Utility.Settings
{
    public interface IVolumeSettings
    {
        public float MasterVolume { get; set; }
        public float BgmVolume { get; set; }
        public float CalculatedBgmVolume { get; }
        public float SfxVolume { get; set; }
        public float CalculatedSfxVolume { get; }
        public float EnvironmentVolume { get; set; }
        public float CalculatedEnvironmentVolume { get; }

        public void ResetVolumeAll();
        public void ResetMasterVolume();
        public void ResetBgmVolume();
        public void ResetSfxVolume();
        public void ResetEnvironmentVolume();
    }
}
