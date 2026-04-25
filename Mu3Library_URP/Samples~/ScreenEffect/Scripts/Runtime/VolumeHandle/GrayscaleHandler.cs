using Mu3Library.URP.ScreenEffect.Effects.Grayscale;
using UnityEngine;

namespace Mu3Library.URP.Sample.ScreenEffect.VolumeHandle
{
    public class GrayscaleHandler : ScreenEffectHandler<GrayscaleEffect>
    {



        protected override void OnInit()
        {
            base.OnInit();

            SetValueWeight(0f);
        }

        #region UI Event
        public void SetValueWeight(float value)
        {
            _effect?.SetWeight(Mathf.Lerp(GrayscalePass.WeightMin, GrayscalePass.WeightMax, value));
        }
        #endregion
    }
}