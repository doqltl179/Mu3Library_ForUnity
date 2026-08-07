using System;

namespace Mu3Library.Sample.Game.WatermelonGame.Service
{
    public interface IWatermelonGameManagerEventBus
    {
        public event Action OnBoardPrepared;
        public event Action OnGameStarted;
        public event Action OnGameEnded;

        public event Action<int> OnScoreChanged;
    }
}
