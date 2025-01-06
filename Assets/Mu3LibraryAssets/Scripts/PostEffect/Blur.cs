using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace Mu3Library.PostEffect {
    [System.Serializable]
    [PostProcess(typeof(BlurEffect), PostProcessEvent.AfterStack, "Mu3Library/Blur")]
    public sealed class Blur : PostProcessEffectSettings {
        [Range(0, 10)] public FloatParameter blurAmount = new FloatParameter { value = 5.0f };
        [Range(0, 1)] public FloatParameter blurStrength = new FloatParameter { value = 1.0f };
        [Range(1, 10)] public IntParameter blurKernelSize = new IntParameter { value = 2 };
    }

    public class BlurEffect : PostProcessEffectRenderer<Blur> {
        private Shader shader;

        public override void Init() {
            shader = Shader.Find("Mu3Library/PostEffect/Blur");
        }

        public override void Render(PostProcessRenderContext context) {
            var sheet = context.propertySheets.Get(shader);
            sheet.properties.SetFloat("_BlurAmount", settings.blurAmount);
            sheet.properties.SetFloat("_Strength", settings.blurStrength);
            sheet.properties.SetInteger("_KernelSize", settings.blurKernelSize);
            context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);
        }
    }
}
