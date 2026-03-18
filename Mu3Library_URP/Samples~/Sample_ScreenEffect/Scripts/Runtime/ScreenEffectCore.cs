using Mu3Library.DI;
using Mu3Library.URP.ScreenEffect;
using UnityEngine;

namespace Mu3Library.URP.Sample.ScreenEffect
{
    public class ScreenEffectCore : CoreBase
    {
        [Inject] private IScreenEffectManager _screenEffectManager;



        protected override void ConfigureContainer()
        {
            RegisterClass<ScreenEffectManager>();
        }
    }
}