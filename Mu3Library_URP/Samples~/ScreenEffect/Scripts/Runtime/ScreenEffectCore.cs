using Mu3Library.DI;
using Mu3Library.URP.Sample.ScreenEffect.VolumeHandle;
using Mu3Library.URP.ScreenEffect;
using UnityEngine;

namespace Mu3Library.URP.Sample.ScreenEffect
{
    public class ScreenEffectCore : CoreBase
    {
        [Inject] private IScreenEffectManager _screenEffectManager;

        [Space(20)]
        [SerializeField] private Camera _mainCamera;

        [Space(20)]
        [SerializeField] private GrayscaleHandler _grayscaleHandler;
        [SerializeField] private ShakeHandler _shakeHandler;

        private GrayscaleEffect _grayscaleEffect;
        private ShakeEffect _shakeEffect;



        protected override void ConfigureContainer()
        {
            RegisterClass<ScreenEffectManager>();
        }

        protected override void Start()
        {
            base.Start();

            _grayscaleEffect = new GrayscaleEffect();
            _shakeEffect = new ShakeEffect();

            _grayscaleHandler.Init(_grayscaleEffect);
            _shakeHandler.Init(_shakeEffect);

            _screenEffectManager.RegisterPass(_grayscaleEffect, CameraFilter);
            _screenEffectManager.RegisterPass(_shakeEffect, CameraFilter);
        }

        protected override void OnDestroy()
        {
            _screenEffectManager?.UnregisterPass(_grayscaleEffect);
            _screenEffectManager?.UnregisterPass(_shakeEffect);

            _grayscaleEffect?.Dispose();
            _shakeEffect?.Dispose();
        }

        private bool CameraFilter(Camera cam)
        {
            return _mainCamera == null || cam == _mainCamera;
        }
    }
}