using Mu3Library.URP.ScreenEffect.Effects.Grayscale;

namespace Mu3Library.URP.Sample.ScreenEffect.VolumeHandle
{
    public class GrayscaleHandler : ScreenEffectHandler<GrayscaleEffect>
    {



        protected override void OnInit()
        {
            base.OnInit();

            _effect.SetWeight(0f);
        }

        #region UI Event
        public void SetValueWeight(float value)
        {
            _effect?.SetWeight(value);
        }
        #endregion
    }
}