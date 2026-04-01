using Mu3Library.DI;
using Mu3Library.URP.ScreenEffect;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mu3Library.URP.Sample.ScreenEffect
{
    public class ScreenEffectCore : CoreBase
    {
        [Inject] private IPostVolumeManager _postVolumeManager;

        [SerializeField] private Volume _volume;



        protected override void ConfigureContainer()
        {
            RegisterClass<PostVolumeManager>();
        }

        protected override void Start()
        {
            base.Start();

            var grayscaleHandler = _postVolumeManager.Wrap<GrayscaleVolume>(_volume);
        }
    }
}