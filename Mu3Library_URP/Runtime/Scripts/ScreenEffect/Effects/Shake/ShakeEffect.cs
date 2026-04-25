using UnityEngine.Rendering.Universal;

namespace Mu3Library.URP.ScreenEffect.Effects.Shake
{
    public class ShakeEffect : ScreenEffectBase<ShakePass>
    {



        public ShakeEffect()
        {
            SetRenderPassEvent(RenderPassEvent.BeforeRenderingPostProcessing);
        }

        public void SetWeight(float weight) => _pass.SetWeight(weight);

        public void SetAmplitude(float amplitude) => _pass.SetAmplitude(amplitude);

        public void SetPeriod(float period) => _pass.SetPeriod(period);
    }
}
