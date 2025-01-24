using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Mu3Library.URP {
    public class ToonRenderPassFeature : ScriptableRendererFeature {
        /// <summary>
        /// The settings used for the Render Objects renderer feature.
        /// </summary>
        [SerializeField] private ToonSettings settings = new ToonSettings();

        private ToonRenderPass renderPass;



        /// <inheritdoc/>
        public override void Create() {
            Debug.Log("Feature Func [ Create ] Start.");

            if(settings.Material == null) {
                const string shaderName = "Mu3Library/URP/RenderFeature/ToonShading";

                Shader shader = Shader.Find(shaderName);
                if(shader == null) {
                    Debug.LogWarning($"Shader not found. name: {shaderName}");

                    return;
                }

                settings.Material = new Material(shader);
            }

            renderPass = new ToonRenderPass(settings.Material, settings.Mesh);

            Debug.Log("Feature Func [ Create ] End.");
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
            //Debug.Log("Feature Func [ AddRenderPasses ] Start.");

            renderer.EnqueuePass(renderPass);

            //Debug.Log("Feature Func [ AddRenderPasses ] End.");
        }



        private class ToonRenderPass : ScriptableRenderPass {
            private Material material;
            private Mesh mesh;



            public ToonRenderPass(Material material, Mesh mesh) {
                this.material = material;
                this.mesh = mesh;
            }

            /// <summary>
            /// "RecordRenderGraph"에서 설정한 "passData"의 값들이 "ExecutePass"의 "data"로 넘어온다.
            /// </summary>
            private static void ExecutePass(PassData data, RasterGraphContext context) {
                //Debug.Log("Feature Func [ ExecutePass ] Start.");

                RasterCommandBuffer cmd = context.cmd;

                RenderTargetIdentifier shadowmap = BuiltinRenderTextureType.CurrentActive;

                cmd.DrawMesh(data.Mesh, Matrix4x4.identity, data.Material);

                CommandBuffer getBuffer = CommandBufferPool.Get();

                //Debug.Log("Feature Func [ ExecutePass ] End.");
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
                //Debug.Log("Feature Func [ RecordRenderGraph ] Start.");

                const string passName = "Render Toon Pass";

                using(var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData)) {
                    UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0);

                    passData.Material = material;
                    passData.Mesh = mesh;

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
                }

                //Debug.Log("Feature Func [ RecordRenderGraph ] End.");
            }



            /// <summary>
            /// 데이터 저장용 class
            /// </summary>
            private class PassData {
                public Material Material;
                public Mesh Mesh;
            }
        }

        /// <summary>
        /// 파라미터 설정용 class
        /// </summary>
        [System.Serializable]
        public class ToonSettings {
            /// <summary>
            /// Controls when the render pass executes.
            /// </summary>
            public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingShadows;

            public Material Material;

            public Mesh Mesh;
        }
    }
}