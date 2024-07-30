using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace Mu3Library.PostEffect {
    [System.Serializable]
    [PostProcess(typeof(BlurEffectEffect), PostProcessEvent.AfterStack, "_MyCustom/Blur")]
    public sealed class Blur : PostProcessEffectSettings {
        [Range(0, 10)] public FloatParameter blurAmount = new FloatParameter { value = 5.0f };
        [Range(0, 1)] public FloatParameter blurStrength = new FloatParameter { value = 1.0f };
    }

    public class BlurEffectEffect : PostProcessEffectRenderer<Blur> {
        private Shader shader;

        public override void Init() {
            shader = Shader.Find("_MyCustom/PostEffect/Blur");
        }

        public override void Render(PostProcessRenderContext context) {
            var sheet = context.propertySheets.Get(shader);
            sheet.properties.SetFloat("_BlurAmount", settings.blurAmount);
            sheet.properties.SetFloat("_Strength", settings.blurStrength);
            context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);
        }
    }
}
