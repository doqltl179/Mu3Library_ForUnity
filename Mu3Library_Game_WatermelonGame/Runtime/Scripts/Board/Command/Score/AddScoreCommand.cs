using System;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Command.Score
{
    /// <summary>
    /// Pays points into the board score, so a bonus arrives on the same path as a merge and
    /// <br/> reports itself through <see cref="BoardController.OnScoreChanged"/>.
    /// <br/> As a step of a <see cref="Flow.SequenceCommand"/> it is what closes a scripted board
    /// <br/> moment, and a negative amount makes the same command a penalty.
    /// </summary>
    public class AddScoreCommand : BoardCommand
    {
        private readonly IBoardCommandContext _context;

        protected readonly int _amount;
        /// <summary>
        /// The points paid, negative to take points away.
        /// </summary>
        public int Amount => _amount;

        private readonly Action<int> _onComplete;



        /// <param name="context">The board, taken from <see cref="BoardController.CommandContext"/>.</param>
        /// <param name="amount">The points to pay, negative to take points away.</param>
        /// <param name="onComplete">Called with the board score, never on a canceled command.</param>
        public AddScoreCommand(IBoardCommandContext context, int amount, Action<int> onComplete = null)
        {
            _context = context;
            _amount = amount;
            _onComplete = onComplete;
        }

        protected override void OnRun()
        {
            if (_context == null)
            {
                Debug.LogWarning("Points cannot be paid without a board.");
                Cancel();
                return;
            }

            _context.AddScore(_amount);

            Complete();
        }

        protected override void OnComplete()
        {
            _onComplete?.Invoke(_context.Score);
        }
    }
}
