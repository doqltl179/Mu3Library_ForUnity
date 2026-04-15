using Mu3Library.URP.ScreenEffect;

namespace Mu3Library.URP.Sample.ScreenEffect.VolumeHandle
{
    public class GrayscaleHandler : PostVolumeHandler<GrayscaleEffect>
    {
        protected override void OnSetActive(bool active)
        {
            _effect?.SetActive(active);
        }

        #region UI Event
        public void SetValueWeight(float value)
        {
            _effect?.SetWeight(value);
        }
        #endregion
    }
}