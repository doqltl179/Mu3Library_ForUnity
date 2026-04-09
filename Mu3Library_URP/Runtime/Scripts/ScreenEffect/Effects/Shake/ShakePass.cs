using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Mu3Library.URP.ScreenEffect
{
    public class ShakePass : ScriptableRenderPass
    {
        private readonly Material _material;
        private float _weight;
        private float _amplitude;

        private static readonly int WeightId = Shader.PropertyToID("_Weight");
        private static readonly int AmplitudeId = Shader.PropertyToID("_Amplitude");



        public ShakePass(Material material)
        {
            _material = material;
            requiresIntermediateTexture = true;
        }

        public void Setup(float weight, float amplitude)
        {
            _weight = weight;
            _amplitude = amplitude;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
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

            _material.SetFloat(WeightId, _weight);
            _material.SetFloat(AmplitudeId, _amplitude);

            RenderGraphUtils.BlitMaterialParameters para = new(source, destination, _material, 0);
            renderGraph.AddBlitPass(para, passName: "Shake");

            resourceData.cameraColor = destination;
        }
    }
}
