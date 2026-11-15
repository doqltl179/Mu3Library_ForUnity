using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Mu3Library.URP.ScreenEffect.Effects.DepthOutline
{
    public class DepthOutlinePass : ScreenPassBase
    {
        protected override string _shaderPath => "Hidden/Mu3Library/DepthOutline";

        public static float WeightMin => 0f;
        public static float WeightMax => 1f;
        private readonly ClampedFloatParameter _weight = new ClampedFloatParameter(1f, WeightMin, WeightMax);

        public static float DepthThresholdMin => 0f;
        public static float DepthThresholdMax => 1f;
        private readonly ClampedFloatParameter _depthThreshold = new ClampedFloatParameter(0.35f, DepthThresholdMin, DepthThresholdMax);

        public static float OutlineStrengthMin => 0f;
        public static float OutlineStrengthMax => 1f;
        private readonly ClampedFloatParameter _outlineStrength = new ClampedFloatParameter(0.8f, OutlineStrengthMin, OutlineStrengthMax);

        public static float OutlineThicknessMin => 1f;
        public static float OutlineThicknessMax => 4f;
        private readonly ClampedFloatParameter _outlineThickness = new ClampedFloatParameter(1f, OutlineThicknessMin, OutlineThicknessMax);

        private static readonly int WeightId = Shader.PropertyToID("_Weight");
        private static readonly int DepthThresholdId = Shader.PropertyToID("_DepthThreshold");
        private static readonly int OutlineStrengthId = Shader.PropertyToID("_OutlineStrength");
        private static readonly int OutlineThicknessId = Shader.PropertyToID("_OutlineThickness");

        public DepthOutlinePass()
        {
            requiresIntermediateTexture = true;
            ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
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
            destinationDesc.name = "DepthOutlineTempTexture";
            destinationDesc.clearBuffer = false;

            TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

            ApplyMaterialProperties();

            RenderGraphUtils.BlitMaterialParameters para = new(source, destination, _material, 0);
            renderGraph.AddBlitPass(para, passName: "DepthOutline");

            resourceData.cameraColor = destination;
        }

        public void SetWeight(float weight) => _weight.value = weight;

        public void SetDepthThreshold(float depthThreshold) => _depthThreshold.value = depthThreshold;

        public void SetOutlineStrength(float outlineStrength) => _outlineStrength.value = outlineStrength;

        public void SetOutlineThickness(float outlineThickness) => _outlineThickness.value = outlineThickness;

        private void ApplyMaterialProperties()
        {
            _material.SetFloat(WeightId, _weight.value);
            _material.SetFloat(DepthThresholdId, _depthThreshold.value);
            _material.SetFloat(OutlineStrengthId, _outlineStrength.value);
            _material.SetFloat(OutlineThicknessId, _outlineThickness.value);
        }
    }
}