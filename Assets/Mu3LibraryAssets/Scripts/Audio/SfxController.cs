namespace Mu3Library.Audio
{
    public class SfxController : AudioController
    {
        protected override float _categoryVolume => AudioManager.Instance.SfxVolume;
    }
}