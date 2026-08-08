using System;

namespace Mu3Library.Game.WatermelonGame.Board.Command.Flow
{
    /// <summary>
    /// Runs one callback on the board and is done with it.
    /// <br/> It is the shortest way into the command list: a project that only wants something to
    /// <br/> happen at a certain point of a <see cref="SequenceCommand"/> does not have to write a
    /// <br/> command class for it.
    /// </summary>
    public class ActionCommand : BoardCommand
    {
        private readonly Action _action;



        public ActionCommand(Action action)
        {
            _action = action;
        }

        protected override void OnRun()
        {
            _action?.Invoke();

            Complete();
        }
    }
}
