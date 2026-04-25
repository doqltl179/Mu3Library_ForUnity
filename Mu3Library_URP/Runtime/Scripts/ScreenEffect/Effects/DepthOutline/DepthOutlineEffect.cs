using UnityEngine.Rendering.Universal;

namespace Mu3Library.URP.ScreenEffect.Effects.DepthOutline
{
    public class DepthOutlineEffect : ScreenEffectBase<DepthOutlinePass>
    {
        public DepthOutlineEffect()
        {
            SetRenderPassEvent(RenderPassEvent.BeforeRenderingPostProcessing);
        }

        public void SetWeight(float weight) => _pass.SetWeight(weight);

        public void SetDepthThreshold(float depthThreshold) => _pass.SetDepthThreshold(depthThreshold);

        public void SetOutlineStrength(float outlineStrength) => _pass.SetOutlineStrength(outlineStrength);

        public void SetOutlineThickness(float outlineThickness) => _pass.SetOutlineThickness(outlineThickness);
    }
}