using UnityEngine.Rendering.Universal;

namespace Mu3Library.URP.ScreenEffect.Effects.GaussianBlur
{
    public class GaussianBlurEffect : ScreenEffectBase<GaussianBlurPass>
    {
        public GaussianBlurEffect()
        {
            SetRenderPassEvent(RenderPassEvent.BeforeRenderingPostProcessing);
        }

        public void SetWeight(float weight) => _pass.SetWeight(weight);

        public void SetBlurRadius(float blurRadius) => _pass.SetBlurRadius(blurRadius);
    }
}