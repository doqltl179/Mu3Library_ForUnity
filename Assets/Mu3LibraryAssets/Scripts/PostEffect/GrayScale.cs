using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace Mu3Library.PostEffect {
    [System.Serializable]
    [PostProcess(typeof(GrayScaleEffect), PostProcessEvent.AfterStack, "Mu3Library/GrayScale")]
    public sealed class GrayScale : PostProcessEffectSettings {
        [Range(0, 1)] public FloatParameter grayScaleStrength = new FloatParameter { value = 1.0f };
    }

    public class GrayScaleEffect : PostProcessEffectRenderer<GrayScale> {
        private Shader shader;

        public override void Init() {
            shader = Shader.Find("Mu3Library/PostEffect/GrayScale");
        }

        public override void Render(PostProcessRenderContext context) {
            var sheet = context.propertySheets.Get(shader);
            sheet.properties.SetFloat("_Strength", settings.grayScaleStrength);
            context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);
        }
    }
}
