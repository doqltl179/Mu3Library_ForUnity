using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Mu3Library.URP.ScreenEffect.Effects.Shake
{
    public class ShakePass : ScreenPassBase
    {
        protected override string _shaderPath => "Hidden/Mu3Library/Shake";

        private const float Tau = Mathf.PI * 2f;

        public static float WeightMin => 0f;
        public static float WeightMax => 1f;
        private ClampedFloatParameter _weight = new ClampedFloatParameter(1f, WeightMin, WeightMax);

        public static float AmplitudeMin => 0f;
        public static float AmplitudeMax => 0.1f;
        private ClampedFloatParameter _amplitude = new ClampedFloatParameter(0.1f, AmplitudeMin, AmplitudeMax);

        public static float PeriodMin => 0.01f;
        public static float PeriodMax => 100f;
        private ClampedFloatParameter _period = new ClampedFloatParameter(1f, PeriodMin, PeriodMax);

        private static readonly int WeightId = Shader.PropertyToID("_Weight");
        private static readonly int AmplitudeId = Shader.PropertyToID("_Amplitude");
        private static readonly int PeriodId = Shader.PropertyToID("_Period");
        private static readonly int ElapsedTimeId = Shader.PropertyToID("_ElapsedTime");
        private static readonly int PhaseOffsetId = Shader.PropertyToID("_PhaseOffset");

        private float _phaseOffset;



        public ShakePass()
        {
            requiresIntermediateTexture = true;
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
            destinationDesc.name = "ShakeTempTexture";
            destinationDesc.clearBuffer = false;

            TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

            float elapsedTime = Time.time;

            _material.SetFloat(WeightId, _weight.value);
            _material.SetFloat(AmplitudeId, _amplitude.value);
            _material.SetFloat(PeriodId, _period.value);
            _material.SetFloat(ElapsedTimeId, elapsedTime);
            _material.SetFloat(PhaseOffsetId, _phaseOffset);

            RenderGraphUtils.BlitMaterialParameters para = new(source, destination, _material, 0);
            renderGraph.AddBlitPass(para, passName: "Shake");

            resourceData.cameraColor = destination;
        }

        public void SetWeight(float weight) => _weight.value = weight;

        public void SetAmplitude(float amplitude) => _amplitude.value = amplitude;

        public void SetPeriod(float period)
        {
            float clampedPeriod = Mathf.Clamp(period, PeriodMin, PeriodMax);
            float elapsedTime = Time.time;
            float currentPhase = GetPhase(elapsedTime, _period.value, _phaseOffset);

            _period.value = clampedPeriod;
            _phaseOffset = Mathf.Repeat(currentPhase - GetPhase(elapsedTime, _period.value, 0f), Tau);
        }

        private static float GetPhase(float elapsedTime, float period, float phaseOffset)
        {
            return Tau * (elapsedTime / Mathf.Max(period, PeriodMin)) + phaseOffset;
        }
    }
}
