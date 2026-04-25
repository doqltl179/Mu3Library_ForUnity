using Mu3Library.URP.ScreenEffect.Effects.DepthOutline;
using UnityEngine;

namespace Mu3Library.URP.Sample.ScreenEffect.VolumeHandle
{
    public class DepthOutlineHandler : ScreenEffectHandler<DepthOutlineEffect>
    {
        protected override void OnInit()
        {
            base.OnInit();

            SetValueWeight(0f);
            SetValueDepthThreshold(0.3f);
            SetValueOutlineStrength(0.85f);
            SetValueOutlineThickness(0f);
        }

        #region UI Event
        public void SetValueWeight(float value)
        {
            _effect?.SetWeight(Mathf.Lerp(DepthOutlinePass.WeightMin, DepthOutlinePass.WeightMax, value));
        }

        public void SetValueDepthThreshold(float value)
        {
            _effect?.SetDepthThreshold(Mathf.Lerp(DepthOutlinePass.DepthThresholdMin, DepthOutlinePass.DepthThresholdMax, value));
        }

        public void SetValueOutlineStrength(float value)
        {
            _effect?.SetOutlineStrength(Mathf.Lerp(DepthOutlinePass.OutlineStrengthMin, DepthOutlinePass.OutlineStrengthMax, value));
        }

        public void SetValueOutlineThickness(float value)
        {
            _effect?.SetOutlineThickness(Mathf.Lerp(DepthOutlinePass.OutlineThicknessMin, DepthOutlinePass.OutlineThicknessMax, value));
        }
        #endregion
    }
}