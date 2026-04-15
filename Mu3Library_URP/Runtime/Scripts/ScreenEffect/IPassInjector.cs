using System;
using UnityEngine.Rendering.Universal;

namespace Mu3Library.URP.ScreenEffect
{
    /// <summary>
    /// RenderPipelineManager 방식으로 렌더 패스를 주입할 때 사용하는 인터페이스.
    /// <br/> PostVolumeManager.RegisterPass() 로 등록하면, 매 프레임 카메라 렌더링 시
    /// TrySetup() 이 호출되어 조건을 만족할 경우 Pass 가 해당 프레임에 주입됩니다.
    /// <br/>
    /// <br/> <b>RenderPassEvent 주요 값 설명:</b>
    /// <br/> - <b>AfterRenderingOpaques</b>: 불투명 오브젝트 렌더링 후 (투명·포스트프로세싱 미적용 상태)
    /// <br/> - <b>AfterRenderingTransparents</b>: 투명 오브젝트까지 렌더링된 후
    /// <br/> - <b>BeforeRenderingPostProcessing</b>: URP 내장 포스트프로세싱 처리 전
    /// <br/> - <b>AfterRenderingPostProcessing</b>: URP 내장 포스트프로세싱 완료 후 (전체 화면 효과에 권장)
    /// <br/> - <b>AfterRendering</b>: 모든 렌더링 완료 후, UI Camera Overlay 포함
    /// </summary>
    public interface IPassInjector : IDisposable
    {
        ScriptableRenderPass Pass { get; }

        /// <summary>
        /// 볼륨 상태를 확인하고 패스에 파라미터를 설정합니다.
        /// </summary>
        /// <returns>패스를 이번 프레임에 등록해야 하면 true, 건너뛰어야 하면 false.</returns>
        bool TrySetup();
    }
}
