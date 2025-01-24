using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Mu3Library.URP {
    class ToonRenderPass : ScriptableRenderPass {
        private Material material;



        public ToonRenderPass(Material material) {
            this.material = material;
        }

        private static void ExecutePass(PassData data, RasterGraphContext context) {

        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
            const string passName = "Render Toon Pass";

            using(var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData)) {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
            }
        }



        private class PassData {

        }
    }
}