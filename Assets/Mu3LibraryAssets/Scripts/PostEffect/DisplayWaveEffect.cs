using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace Mu3Library.PostEffect {
    [System.Serializable]
    [PostProcess(typeof(DisplayWaveEffect), PostProcessEvent.AfterStack, "_MyCustom/DisplayWave")]
    public sealed class DisplayWave : PostProcessEffectSettings {
        //public FloatParameter frequency = new FloatParameter { value = 1.0f };
        //public FloatParameter amplitude = new FloatParameter { value = 0.1f };
        //public FloatParameter speed = new FloatParameter { value = 1.0f };
        //public Vector2Parameter center = new Vector2Parameter { value = new Vector2(0.5f, 0.5f) };
        //public FloatParameter radius = new FloatParameter { value = 0.2f };
        public Vector2Parameter center = new Vector2Parameter { value = Vector2.one * 0.5f };
    }

    public class DisplayWaveEffect : PostProcessEffectRenderer<DisplayWave> {
        private Shader shader;



        public override void Init() {
            shader = Shader.Find("_MyCustom/PostEffect/DisplayWave");
        }

        public override void Render(PostProcessRenderContext context) {
            var sheet = context.propertySheets.Get(shader);
            //sheet.properties.SetFloat("_Frequency", settings.frequency);
            //sheet.properties.SetFloat("_Amplitude", settings.amplitude);
            //sheet.properties.SetFloat("_Speed", settings.speed);
            //sheet.properties.SetVector("_Center", new Vector4(settings.center.value.x, settings.center.value.y, 0, 0));
            //sheet.properties.SetFloat("_Radius", settings.radius);
            sheet.properties.SetVector("_Center", settings.center);
            context.command.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);
        }
    }
}