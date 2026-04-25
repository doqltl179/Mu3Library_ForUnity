using Mu3Library.DI;
using Mu3Library.URP.Sample.ScreenEffect.VolumeHandle;
using Mu3Library.URP.ScreenEffect;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.URP.Sample.ScreenEffect
{
    public class ScreenEffectCore : CoreBase
    {
        [Inject] private IScreenEffectManager _screenEffectManager;

        [Space(20)]
        [SerializeField] private Camera _mainCamera;

        [Space(20)]
        [SerializeField] private ToggleGroup _toggleItemGroup;

        [Space(20)]
        [SerializeField] private GrayscaleHandler _grayscaleHandler;
        [SerializeField] private ShakeHandler _shakeHandler;
        [SerializeField] private GaussianBlurHandler _gaussianBlurHandler;
        [SerializeField] private DepthOutlineHandler _depthOutlineHandler;



        protected override void ConfigureContainer()
        {
            RegisterClass<ScreenEffectManager>();
        }

        protected override void Start()
        {
            base.Start();

            SetupHandler(_grayscaleHandler);
            SetupHandler(_shakeHandler);
            SetupHandler(_gaussianBlurHandler);
            SetupHandler(_depthOutlineHandler);

            _toggleItemGroup.ActiveToggles();
            _toggleItemGroup.SetAllTogglesOff();
        }

        protected override void OnDestroy()
        {
            DisposeHandler(_grayscaleHandler);
            DisposeHandler(_shakeHandler);
            DisposeHandler(_gaussianBlurHandler);
            DisposeHandler(_depthOutlineHandler);
        }

        private void SetupHandler<TEffect>(ScreenEffectHandler<TEffect> handler) where TEffect : IScreenEffect, new()
        {
            if (handler == null)
            {
                return;
            }

            handler.Context(_screenEffectManager, _mainCamera);
            handler.Init();
            handler.RegisterEffect();
        }

        private static void DisposeHandler<TEffect>(ScreenEffectHandler<TEffect> handler) where TEffect : IScreenEffect, new()
        {
            if (handler == null)
            {
                return;
            }

            handler.UnregisterEffect(true);
        }
    }
}