using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Mu3Library.URP.ScreenEffect.Effects.Grayscale
{
    public class GrayscaleEffect : ScreenEffectBase<GrayscalePass>
    {




        public GrayscaleEffect()
        {
            SetRenderPassEvent(RenderPassEvent.BeforeRenderingPostProcessing);
        }

        public void SetWeight(float weight) => _pass.SetWeight(weight);
    }
}
