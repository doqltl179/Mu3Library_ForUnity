using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Mu3Library.URP.ScreenEffect
{
    public class GrayscaleFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class GrayscaleSettings
        {
            public RenderPassEvent Event = RenderPassEvent.AfterRenderingOpaques;
        }

        [SerializeField] private GrayscaleSettings _settings = new GrayscaleSettings();

        private const string ShaderPath = "Hidden/Mu3Library/Grayscale";

        private Material _material;
        private GrayscalePass _pass;

        private GrayscaleVolume m_volume;
        private GrayscaleVolume _volume
        {
            get
            {
                if (m_volume == null)
                {
                    m_volume = VolumeManager.instance.stack.GetComponent<GrayscaleVolume>();
                }

                return m_volume;
            }
        }



        public override void Create()
        {
            Debug.Log("GrayscaleFeature Create called.");

            Shader shader = Shader.Find(ShaderPath);
            if (shader == null)
            {
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(shader);
            _pass = new GrayscalePass(_material)
            {
                renderPassEvent = _settings.Event
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_material == null || _pass == null)
            {
                return;
            }

            if (!renderingData.cameraData.postProcessEnabled)
            {
                return;
            }

            if (_volume == null || !_volume.IsActive)
            {
                return;
            }

            _pass.Setup(_volume.Weight.value);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            Debug.Log("GrayscaleFeature Dispose called.");

            CoreUtils.Destroy(_material);
        }
    }
}