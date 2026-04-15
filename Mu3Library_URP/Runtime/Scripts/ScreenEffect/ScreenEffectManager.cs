using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Mu3Library.URP.ScreenEffect
{
    public class ScreenEffectManager : IScreenEffectManager
    {
        private struct PassEntry
        {
            public IPassInjector Injector;
            public Func<Camera, bool> Filter;
        }

        private readonly List<PassEntry> _injectors = new List<PassEntry>();



        public void RegisterPass(IPassInjector injector, Func<Camera, bool> cameraFilter = null)
        {
            if (_injectors.Exists(e => e.Injector == injector))
            {
                return;
            }

            if (_injectors.Count == 0)
            {
                RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            }

            _injectors.Add(new PassEntry { Injector = injector, Filter = cameraFilter });
        }

        public void UnregisterPass(IPassInjector injector)
        {
            int index = _injectors.FindIndex(e => e.Injector == injector);
            if (index < 0)
            {
                return;
            }

            _injectors.RemoveAt(index);

            if (_injectors.Count == 0)
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
            foreach (var entry in _injectors)
            {
                if (entry.Filter != null && !entry.Filter(camera))
                {
                    continue;
                }

                if (entry.Injector.TrySetup())
                {
                    renderer.EnqueuePass(entry.Injector.Pass);
                }
            }
        }
    }
}

