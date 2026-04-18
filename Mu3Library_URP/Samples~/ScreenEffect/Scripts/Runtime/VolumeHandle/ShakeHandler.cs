using Mu3Library.URP.ScreenEffect.Effects.Shake;

namespace Mu3Library.URP.Sample.ScreenEffect.VolumeHandle
{
    public class ShakeHandler : ScreenEffectHandler<ShakeEffect>
    {



        protected override void OnInit()
        {
            base.OnInit();

            _effect.SetWeight(0f);
            _effect.SetAmplitude(0f);
        }

        #region UI Event
        public void SetValueWeight(float value)
        {
            _effect?.SetWeight(value);
        }

        public void SetValueAmplitude(float value)
        {
            _effect?.SetAmplitude(value);
        }
        #endregion
    }
}
