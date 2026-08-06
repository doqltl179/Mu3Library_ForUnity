using Mu3Library.DI;
using Mu3Library.Sample.Game.WatermelonGame.Service;
using UnityEngine;

namespace Mu3Library.Sample.Game.WatermelonGame
{
    public class WatermelonGameCore : CoreBase
    {
        [Inject] protected IWatermelonGameManager _gameManager;
        [Inject] protected IWatermelonGameManagerEventBus _gameManagerEventBus;



        protected override void ConfigureContainer()
        {
            RegisterClass<WatermelonGameManager>();
        }

        protected override void Start()
        {
            base.Start();

            _gameManager.Prepare();
        }
    }
}
