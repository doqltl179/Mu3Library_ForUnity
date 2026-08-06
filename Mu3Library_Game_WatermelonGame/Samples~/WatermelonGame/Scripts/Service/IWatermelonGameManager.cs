using Mu3Library.Game.WatermelonGame.Board;

namespace Mu3Library.Sample.Game.WatermelonGame.Service
{
    public interface IWatermelonGameManager
    {
        public void Prepare(BoardSnapshot snapshot = null);
    }
}
