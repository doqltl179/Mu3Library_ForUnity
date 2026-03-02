namespace Mu3Library.Audio
{
    public class EnvironmentController : AudioController
    {
        protected override float _categoryVolume => _audioVolumeSettings != null ? _audioVolumeSettings.EnvironmentVolume : 1.0f;
    }
}
