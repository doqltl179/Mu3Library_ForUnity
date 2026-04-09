using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Mu3Library.URP.ScreenEffect
{
    public class GrayscalePass : ScriptableRenderPass
    {
        private readonly Material _material;
        private float _weight;

        private static readonly int WeightId = Shader.PropertyToID("_Weight");



        public GrayscalePass(Material material)
        {
            _material = material;
            requiresIntermediateTexture = true;
        }

        public void Setup(float weight) => _weight = weight;

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
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

            _material.SetFloat(WeightId, _weight);

            RenderGraphUtils.BlitMaterialParameters para = new(source, destination, _material, 0);
            renderGraph.AddBlitPass(para, passName: "Grayscale");

            resourceData.cameraColor = destination;
        }
    }
}