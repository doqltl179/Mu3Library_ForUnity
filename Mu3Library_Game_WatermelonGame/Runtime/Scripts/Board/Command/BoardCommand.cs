namespace Mu3Library.Game.WatermelonGame.Board.Command
{
    public abstract class BoardCommand : IBoardCommand
    {
        protected bool _isRunning;
        public bool IsRunning => _isRunning;

        protected bool _isCompleted;
        public bool IsCompleted => _isCompleted;



        public abstract void Run();

        public abstract void Dispose();
    }
}