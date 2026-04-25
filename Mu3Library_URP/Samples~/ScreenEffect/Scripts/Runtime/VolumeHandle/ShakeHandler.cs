using Mu3Library.URP.ScreenEffect.Effects.Shake;
using UnityEngine;

namespace Mu3Library.URP.Sample.ScreenEffect.VolumeHandle
{
    public class ShakeHandler : ScreenEffectHandler<ShakeEffect>
    {



        protected override void OnInit()
        {
            base.OnInit();

            SetValueWeight(0f);
            SetValueAmplitude(0f);
            SetValuePeriod(0f);
        }

        #region UI Event
        public void SetValueWeight(float value)
        {
            _effect?.SetWeight(Mathf.Lerp(ShakePass.WeightMin, ShakePass.WeightMax, value));
        }

        public void SetValueAmplitude(float value)
        {
            _effect?.SetAmplitude(Mathf.Lerp(ShakePass.AmplitudeMin, ShakePass.AmplitudeMax, value));
        }

        public void SetValuePeriod(float value)
        {
            _effect?.SetPeriod(Mathf.Lerp(ShakePass.PeriodMin, ShakePass.PeriodMax, value));
        }
        #endregion
    }
}
