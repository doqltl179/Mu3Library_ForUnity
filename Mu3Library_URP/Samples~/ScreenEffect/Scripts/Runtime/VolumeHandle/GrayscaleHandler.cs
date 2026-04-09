using Mu3Library.URP.ScreenEffect;

namespace Mu3Library.URP.Sample.ScreenEffect.VolumeHandle
{
    public class GrayscaleHandler : PostVolumeHandler<GrayscaleVolume>
    {




        #region UI Event
        public void SetValueWeight(float value)
        {
            if (_handler == null)
            {
                return;
            }

            _handler.Component.Weight.value = value;
        }
        #endregion
    }
}