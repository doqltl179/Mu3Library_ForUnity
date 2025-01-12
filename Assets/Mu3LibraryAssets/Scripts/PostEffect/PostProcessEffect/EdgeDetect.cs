using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace Mu3Library.PostEffect.PostProcessEffect {
    [System.Serializable]
    [PostProcess(typeof(EdgeDetectEffect), PostProcessEvent.AfterStack, "Mu3Library/EdgeDetect")]
    public sealed class EdgeDetect : PostProcessEffectSettings {
        public ColorParameter EdgeColor = new ColorParameter() { value = Color.black };
        [Range(0.1f, 10)] public FloatParameter EdgeThickness = new FloatParameter { value = 1.0f };
    }

    public class EdgeDetectEffect : PostProcessEffectRenderer<EdgeDetect> {
        private Shader shader;

        public override void Init() {
            shader = Shader.Find("Mu3Library/PostEffect/PostProcessEffect/EdgeDetect");
        }

        public override void Render(PostProcessRenderContext context) {
            var sheet = context.propertySheets.Get(shader);
            sheet.properties.SetColor("_EdgeColor", settings.EdgeColor);
            sheet.properties.SetFloat("_EdgeThickness", settings.EdgeThickness);
            context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);
        }
    }
}
