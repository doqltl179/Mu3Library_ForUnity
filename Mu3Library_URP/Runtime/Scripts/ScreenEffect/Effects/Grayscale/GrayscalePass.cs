using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Mu3Library.URP.ScreenEffect.Effects.Grayscale
{
    public class GrayscalePass : ScreenPassBase
    {
        protected override string _shaderPath => "Hidden/Mu3Library/Grayscale";

        private ClampedFloatParameter _weight = new ClampedFloatParameter(1f, 0f, 1f);

        private static readonly int WeightId = Shader.PropertyToID("_Weight");



        public GrayscalePass()
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
            destinationDesc.name = "GrayscaleTempTexture";
            destinationDesc.clearBuffer = false;

            TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

            _material.SetFloat(WeightId, _weight.value);

            RenderGraphUtils.BlitMaterialParameters para = new(source, destination, _material, 0);
            renderGraph.AddBlitPass(para, passName: "Grayscale");

            resourceData.cameraColor = destination;
        }

        public void SetWeight(float weight) => _weight.value = weight;
    }
}