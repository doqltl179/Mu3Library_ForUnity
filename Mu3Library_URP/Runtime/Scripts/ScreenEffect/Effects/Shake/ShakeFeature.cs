using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Mu3Library.URP.ScreenEffect
{
    public class ShakeFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class ShakeSettings
        {
            public RenderPassEvent Event = RenderPassEvent.AfterRenderingOpaques;
        }

        [SerializeField] private ShakeSettings _settings = new ShakeSettings();

        private Material _material;
        private ShakePass _pass;

        private const string ShaderPath = "Hidden/Mu3Library/Shake";

        private ShakeVolume m_volume;
        private ShakeVolume _volume
        {
            get
            {
                if (m_volume == null)
                {
                    m_volume = VolumeManager.instance.stack.GetComponent<ShakeVolume>();
                }

                return m_volume;
            }
        }



        public override void Create()
        {
            Debug.Log("ShakeFeature Create called.");

            Shader shader = Shader.Find(ShaderPath);
            if (shader == null)
            {
                Debug.LogError($"[ShakeFeature] Shader not found: {ShaderPath}");
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(shader);
            _pass = new ShakePass(_material)
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

            _pass.Setup(_volume.Weight.value, _volume.Amplitude.value);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            Debug.Log("ShakeFeature Dispose called.");

            CoreUtils.Destroy(_material);
        }
    }
}
