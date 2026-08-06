using System;

namespace Mu3Library.Sample.Game.WatermelonGame.Service
{
    public interface IWatermelonGameManagerEventBus
    {
        public event Action OnBoardPrepared;
    }
}
