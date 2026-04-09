using Mu3Library.DI;
using Mu3Library.URP.Sample.ScreenEffect.VolumeHandle;
using Mu3Library.URP.ScreenEffect;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mu3Library.URP.Sample.ScreenEffect
{
    public class ScreenEffectCore : CoreBase
    {
        [Inject] private IPostVolumeManager _postVolumeManager;

        [Space(20)]
        [SerializeField] private Volume _volume;

        [Space(20)]
        [SerializeField] private GrayscaleHandler _grayscaleHandler;
        [SerializeField] private ShakeHandler _shakeHandler;



        protected override void ConfigureContainer()
        {
            RegisterClass<PostVolumeManager>();
        }

        protected override void Start()
        {
            base.Start();

            _grayscaleHandler.Context(_postVolumeManager, _volume);
            _shakeHandler.Context(_postVolumeManager, _volume);
        }
    }
}