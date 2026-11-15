namespace Mu3Library.Audio
{
    public class BgmController : AudioController
    {
        protected override float _categoryVolume => _audioVolumeSettings != null ? _audioVolumeSettings.BgmVolume : 1.0f;
    }
}
