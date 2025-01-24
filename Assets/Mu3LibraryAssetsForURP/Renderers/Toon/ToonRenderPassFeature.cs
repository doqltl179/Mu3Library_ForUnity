using UnityEngine;
using UnityEngine.Rendering;
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
            if(settings.Material == null) {
                const string shaderName = "Mu3Library/URP/RenderFeature/ToonShading";

                Shader shader = Shader.Find(shaderName);
                if(shader == null) {
                    Debug.LogWarning($"Shader not found. name: {shaderName}");

                    return;
                }

                settings.Material = new Material(shader);
            }

            renderPass = new ToonRenderPass(settings.Material);
        }

        /// <inheritdoc/>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
            renderer.EnqueuePass(renderPass);
        }



        [System.Serializable]
        public class ToonSettings {
            /// <summary>
            /// Controls when the render pass executes.
            /// </summary>
            public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingShadows;

            public Material Material;
        }
    }
}