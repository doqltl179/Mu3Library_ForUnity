using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Mu3Library.URP.ScreenEffect.Effects.Shake
{
    public class ShakePass : ScreenPassBase
    {
        protected override string _shaderPath => "Hidden/Mu3Library/Shake";

        private ClampedFloatParameter _weight = new ClampedFloatParameter(1f, 0f, 1f);
        private ClampedFloatParameter _amplitude = new ClampedFloatParameter(0.1f, 0f, 0.1f);

        private static readonly int WeightId = Shader.PropertyToID("_Weight");
        private static readonly int AmplitudeId = Shader.PropertyToID("_Amplitude");



        public ShakePass()
        {
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null)
            {
                return;
            }

            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer)
            {
                return;
            }

            TextureHandle source = resourceData.activeColorTexture;

            var destinationDesc = renderGraph.GetTextureDesc(source);
            destinationDesc.name = "ShakeTempTexture";
            destinationDesc.clearBuffer = false;

            TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

            _material.SetFloat(WeightId, _weight.value);
            _material.SetFloat(AmplitudeId, _amplitude.value);

            RenderGraphUtils.BlitMaterialParameters para = new(source, destination, _material, 0);
            renderGraph.AddBlitPass(para, passName: "Shake");

            resourceData.cameraColor = destination;
        }

        public void SetWeight(float weight) => _weight.value = weight;

        public void SetAmplitude(float amplitude) => _amplitude.value = amplitude;
    }
}
