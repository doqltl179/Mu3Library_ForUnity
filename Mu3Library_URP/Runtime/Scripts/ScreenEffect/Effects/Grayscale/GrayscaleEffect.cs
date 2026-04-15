using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Mu3Library.URP.ScreenEffect
{
    /// <summary>
    /// Grayscale 화면 효과를 RenderPipelineManager 방식으로 주입하는 클래스.
    /// </summary>
    public class GrayscaleEffect : IPassInjector
    {
        private const string ShaderPath = "Hidden/Mu3Library/Grayscale";

        private readonly Material _material;
        private readonly GrayscalePass _pass;

        private bool _isActive = false;
        private float _weight = 1f;

        public ScriptableRenderPass Pass => _pass;



        public GrayscaleEffect(RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingPostProcessing)
        {
            Shader shader = Shader.Find(ShaderPath);
            if (shader == null)
            {
                Debug.LogError($"[GrayscaleEffect] Shader not found: {ShaderPath}");
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(shader);
            _pass = new GrayscalePass(_material) { renderPassEvent = passEvent };
        }

        public void SetActive(bool active) => _isActive = active;

        public void SetWeight(float weight) => _weight = weight;

        public bool TrySetup()
        {
            if (_material == null || _pass == null || !_isActive)
            {
                return false;
            }

            _pass.Setup(_weight);
            return true;
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_material);
        }
    }
}
