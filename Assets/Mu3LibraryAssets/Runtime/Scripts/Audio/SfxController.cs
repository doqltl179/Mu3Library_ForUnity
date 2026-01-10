namespace Mu3Library.Audio
{
    public class SfxController : AudioController
    {
        protected override float _categoryVolume => _audioVolumeSettings != null ? _audioVolumeSettings.SfxVolume : 1.0f;
    }
}
