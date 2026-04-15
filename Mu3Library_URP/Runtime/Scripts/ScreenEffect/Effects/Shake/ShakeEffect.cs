using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Mu3Library.URP.ScreenEffect
{
    /// <summary>
    /// Shake 화면 효과를 RenderPipelineManager 방식으로 주입하는 클래스.
    /// </summary>
    public class ShakeEffect : IPassInjector
    {
        private const string ShaderPath = "Hidden/Mu3Library/Shake";

        private readonly Material _material;
        private readonly ShakePass _pass;

        private bool _isActive = false;
        private float _weight = 1f;
        private float _amplitude = 0.1f;

        public ScriptableRenderPass Pass => _pass;



        public ShakeEffect(RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingPostProcessing)
        {
            Shader shader = Shader.Find(ShaderPath);
            if (shader == null)
            {
                Debug.LogError($"[ShakeEffect] Shader not found: {ShaderPath}");
                return;
            }

            _material = CoreUtils.CreateEngineMaterial(shader);
            _pass = new ShakePass(_material) { renderPassEvent = passEvent };
        }

        public void SetActive(bool active) => _isActive = active;

        public void SetWeight(float weight) => _weight = weight;

        public void SetAmplitude(float amplitude) => _amplitude = amplitude;

        public bool TrySetup()
        {
            if (_material == null || _pass == null || !_isActive)
            {
                return false;
            }

            _pass.Setup(_weight, _amplitude);
            return true;
        }

        public void Dispose()
        {
            CoreUtils.Destroy(_material);
        }
    }
}
