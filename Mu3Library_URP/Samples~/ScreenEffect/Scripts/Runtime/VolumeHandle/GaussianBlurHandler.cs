using Mu3Library.URP.ScreenEffect.Effects.GaussianBlur;
using UnityEngine;

namespace Mu3Library.URP.Sample.ScreenEffect.VolumeHandle
{
    public class GaussianBlurHandler : ScreenEffectHandler<GaussianBlurEffect>
    {
        protected override void OnInit()
        {
            base.OnInit();

            SetValueWeight(0f);
            SetValueBlurRadius(0.3f);
        }

        #region UI Event
        public void SetValueWeight(float value)
        {
            _effect?.SetWeight(Mathf.Lerp(GaussianBlurPass.WeightMin, GaussianBlurPass.WeightMax, value));
        }

        public void SetValueBlurRadius(float value)
        {
            _effect?.SetBlurRadius(Mathf.Lerp(GaussianBlurPass.BlurRadiusMin, GaussianBlurPass.BlurRadiusMax, value));
        }
        #endregion
    }
}