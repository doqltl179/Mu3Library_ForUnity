using System;
using UnityEngine;

namespace Mu3Library.URP.ScreenEffect
{
    /// <summary>
    /// 렌더 패스 주입을 관리하는 인터페이스.
    /// <br/>
    /// <br/> <b>사용 예:</b>
    /// <code>
    /// var effect = new GrayscaleEffect(RenderPassEvent.AfterRenderingPostProcessing);
    /// _screenEffectManager.RegisterPass(effect);
    /// </code>
    /// <br/> <b>cameraFilter 사용 예:</b>
    /// <code>
    /// // MainCamera 에만 적용
    /// _screenEffectManager.RegisterPass(effect, cam => cam.CompareTag("MainCamera"));
    /// </code>
    /// <br/> <b>해제 시:</b>
    /// <code>
    /// _screenEffectManager.UnregisterPass(effect);
    /// effect.Dispose();
    /// </code>
    /// </summary>
    public interface IScreenEffectManager
    {
        /// <summary>
        /// RenderPipelineManager를 통해 매 프레임 렌더 패스를 주입하도록 등록합니다.
        /// </summary>
        /// <param name="cameraFilter">패스를 적용할 카메라 조건. null이면 모든 카메라에 적용.</param>
        void RegisterPass(IPassInjector injector, Func<Camera, bool> cameraFilter = null);

        /// <summary>
        /// 등록된 렌더 패스 주입을 해제합니다.
        /// Dispose()는 직접 호출해야 합니다.
        /// </summary>
        void UnregisterPass(IPassInjector injector);
    }
}
