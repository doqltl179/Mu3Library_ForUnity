using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Mu3Library.URP.ScreenEffect
{
    public class ScreenEffectManager : IScreenEffectManager
    {
        private struct EffectEntry
        {
            public IScreenEffect Effect;
            public Func<Camera, bool> Filter;
        }

        private readonly List<EffectEntry> _effects = new List<EffectEntry>();



        public IScreenEffect RegisterEffect<TEffect>(Func<Camera, bool> cameraFilter = null) where TEffect : IScreenEffect, new()
        {
            TEffect effect = new();
            RegisterEffect(effect, cameraFilter);

            return effect;
        }

        public void RegisterEffect(IScreenEffect effect, Func<Camera, bool> cameraFilter = null)
        {
            if (_effects.Count == 0)
            {
                RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            }

            _effects.Add(new EffectEntry { Effect = effect, Filter = cameraFilter });
        }

        public void UnregisterEffect(IScreenEffect effect)
            => UnregisterEffect(effect, false);

        public void UnregisterEffect(IScreenEffect effect, bool dispose)
        {
            if (effect == null)
            {
                return;
            }

            var entry = _effects.Find(t => t.Effect == effect);
            if (entry.Effect == null)
            {
                return;
            }

            _effects.Remove(entry);

            if (dispose)
            {
                entry.Effect.Dispose();
            }

            if (_effects.Count == 0)
            {
                RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            }
        }

        public void UnregisterEffectAll<TEffect>() where TEffect : IScreenEffect
            => UnregisterEffectAll(typeof(TEffect), false);

        public void UnregisterEffectAll<TEffect>(bool dispose) where TEffect : IScreenEffect
            => UnregisterEffectAll(typeof(TEffect), dispose);

        public void UnregisterEffectAll(Type effectType)
            => UnregisterEffectAll(effectType, false);

        public void UnregisterEffectAll(Type effectType, bool dispose)
        {
            if (effectType == null)
            {
                return;
            }

            for (int i = 0; i < _effects.Count; i++)
            {
                var entry = _effects[i];
                var effect = entry.Effect;
                if (effect == null || effect.PassType != effectType)
                {
                    continue;
                }

                _effects.RemoveAt(i);
                i--;

                if (dispose)
                {
                    effect.Dispose();
                }
            }

            if (_effects.Count == 0)
            {
                RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            }
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            // 에디터 Preview 카메라는 건너뜀
            if (camera.cameraType == CameraType.Preview)
            {
                return;
            }

            var additionalData = camera.GetUniversalAdditionalCameraData();

            // 포스트프로세싱이 비활성화된 카메라는 건너뜀
            if (additionalData == null || !additionalData.renderPostProcessing)
            {
                return;
            }

            var renderer = additionalData.scriptableRenderer;
            foreach (var entry in _effects)
            {
                if (entry.Filter != null && !entry.Filter(camera))
                {
                    continue;
                }

                entry.Effect.RequestEnqueuePass(renderer, context);
            }
        }
    }
}

