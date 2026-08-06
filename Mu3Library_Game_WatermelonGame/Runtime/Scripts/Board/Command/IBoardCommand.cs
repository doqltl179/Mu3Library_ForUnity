using System;

namespace Mu3Library.Game.WatermelonGame.Board.Command
{
    public interface IBoardCommand : IDisposable
    {
        public bool IsRunning { get; }
        public bool IsCompleted { get; }



        public void Run();
    }
}