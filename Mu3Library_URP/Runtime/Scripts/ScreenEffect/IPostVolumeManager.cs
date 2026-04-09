using UnityEngine;
using UnityEngine.Rendering;

namespace Mu3Library.URP.ScreenEffect
{
    public interface IPostVolumeManager
    {
        /// <summary>
        /// 씬에 이미 존재하는 Volume의 VolumeComponent를 핸들러로 감쌉니다.
        /// 컴포넌트가 없으면 자동으로 추가합니다.
        /// Dispose() 시 컴포넌트를 비활성화합니다.
        /// </summary>
        VolumeHandler<T> Wrap<T>(Volume volume) where T : VolumeComponent;

        public VolumeHandler<T> Create<T>() where T : VolumeComponent, new();
        public VolumeHandler<T> Create<T>(float priority) where T : VolumeComponent, new();
        public VolumeHandler<T> Create<T>(bool isGlobal) where T : VolumeComponent, new();
        public VolumeHandler<T> Create<T>(Transform parent) where T : VolumeComponent, new();
        public VolumeHandler<T> Create<T>(float priority, bool isGlobal) where T : VolumeComponent, new();
        public VolumeHandler<T> Create<T>(float priority, Transform parent) where T : VolumeComponent, new();
        public VolumeHandler<T> Create<T>(bool isGlobal, Transform parent) where T : VolumeComponent, new();
        /// <summary>
        /// 새로운 Volume GameObject를 생성하고 핸들러로 반환합니다.
        /// Dispose() 시 GameObject(Volume)를 삭제합니다.
        /// </summary>
        public VolumeHandler<T> Create<T>(float priority, bool isGlobal, Transform parent) where T : VolumeComponent, new();
    }
}
