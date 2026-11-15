using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Mu3Library.URP.ScreenEffect.Effects.GaussianBlur
{
    public class GaussianBlurPass : ScreenPassBase
    {
        protected override string _shaderPath => "Hidden/Mu3Library/GaussianBlur";

        public static float WeightMin => 0f;
        public static float WeightMax => 1f;
        private readonly ClampedFloatParameter _weight = new ClampedFloatParameter(1f, WeightMin, WeightMax);

        public static float BlurRadiusMin => 0f;
        public static float BlurRadiusMax => 6f;
        private readonly ClampedFloatParameter _blurRadius = new ClampedFloatParameter(1.5f, BlurRadiusMin, BlurRadiusMax);

        private static readonly int WeightId = Shader.PropertyToID("_Weight");
        private static readonly int BlurRadiusId = Shader.PropertyToID("_BlurRadius");

        public GaussianBlurPass()
        {
            requiresIntermediateTexture = true;
            ConfigureInput(ScriptableRenderPassInput.Color);
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
            destinationDesc.name = "GaussianBlurTempTexture";
            destinationDesc.clearBuffer = false;

            TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

            ApplyMaterialProperties();

            RenderGraphUtils.BlitMaterialParameters para = new(source, destination, _material, 0);
            renderGraph.AddBlitPass(para, passName: "GaussianBlur");

            resourceData.cameraColor = destination;
        }

        public void SetWeight(float weight) => _weight.value = weight;

        public void SetBlurRadius(float blurRadius) => _blurRadius.value = blurRadius;

        private void ApplyMaterialProperties()
        {
            _material.SetFloat(WeightId, _weight.value);
            _material.SetFloat(BlurRadiusId, _blurRadius.value);
        }
    }
}