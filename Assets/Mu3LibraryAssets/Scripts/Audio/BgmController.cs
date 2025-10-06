namespace Mu3Library.Audio
{
    public class BgmController : AudioController
    {
        protected override float _categoryVolume => AudioManager.Instance.BgmVolume;
    }
}