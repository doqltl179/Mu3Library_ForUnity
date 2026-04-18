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



        protected override void ConfigureContainer()
        {
            RegisterClass<ScreenEffectManager>();
        }

        protected override void Start()
        {
            base.Start();

            _grayscaleHandler.Context(_screenEffectManager, _mainCamera);
            _shakeHandler.Context(_screenEffectManager, _mainCamera);

            _grayscaleHandler.Init();
            _shakeHandler.Init();

            _grayscaleHandler.RegisterEffect();
            _shakeHandler.RegisterEffect();

            _toggleItemGroup.ActiveToggles();
            _toggleItemGroup.SetAllTogglesOff();
        }

        protected override void OnDestroy()
        {
            _grayscaleHandler.UnregisterEffect(true);
            _shakeHandler.UnregisterEffect(true);
        }
    }
}