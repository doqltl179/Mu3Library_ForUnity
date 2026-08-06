using System;

namespace Mu3Library.Game.WatermelonGame.Board.Command
{
    public class BoardCommandHandler : IDisposable
    {
        private BoardController _board;



        public BoardCommandHandler(BoardController board)
        {
            _board = board;
        }

        public void Dispose()
        {

        }
    }
}