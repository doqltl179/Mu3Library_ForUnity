using System;
using Mu3Library.Game.WatermelonGame.Board.Item;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Command.Item
{
    /// <summary>
    /// Grows a single item into a later one of the catalog, where it stands and without a partner
    /// <br/> to merge with. It is the rainbow item of this board: one touch and the fruit becomes
    /// <br/> the next one.
    /// <br/> The command is canceled when the item has nothing above it, so the largest fruit of a
    /// <br/> catalog cannot be promoted out of the board.
    /// </summary>
    public class PromoteItemCommand : BoardCommand
    {
        private readonly IBoardCommandContext _context;
        private readonly BoardItem _item;

        protected readonly int _steps;
        /// <summary>
        /// How many catalog entries the item is moved up, one being the merge result.
        /// </summary>
        public int Steps => _steps;

        protected readonly bool _awardScore;
        /// <summary>
        /// True while the promotion pays out the points a merge into the same item would.
        /// </summary>
        public bool AwardScore => _awardScore;

        protected BoardItem _promotedItem;
        /// <summary>
        /// The item that took the place of the old one, null until the command completed.
        /// </summary>
        public BoardItem PromotedItem => _promotedItem;

        private readonly Action<BoardItem> _onPromoted;



        /// <param name="context">The board, taken from <see cref="BoardController.CommandContext"/>.</param>
        /// <param name="item">The item to grow.</param>
        /// <param name="steps">How many catalog entries to move up, one being the merge result.</param>
        /// <param name="awardScore">True to pay out the points a merge into the same item would.</param>
        /// <param name="onPromoted">Called with the item that took the place of the old one, never on a canceled command.</param>
        public PromoteItemCommand(
            IBoardCommandContext context,
            BoardItem item,
            int steps = 1,
            bool awardScore = true,
            Action<BoardItem> onPromoted = null)
        {
            _context = context;
            _item = item;
            _steps = Mathf.Max(1, steps);
            _awardScore = awardScore;
            _onPromoted = onPromoted;
        }

        protected override void OnRun()
        {
            if (_context == null || _item == null)
            {
                Debug.LogWarning("An item cannot be promoted without a board and an item.");
                Cancel();
                return;
            }

            int itemIndex = _item.Index;
            if (itemIndex < 0)
            {
                Cancel();
                return;
            }

            int promotedIndex = itemIndex + _steps;
            if (promotedIndex >= _context.ItemIndexCount)
            {
                // The last entry of the catalog has nothing above it, the same way the largest
                // fruit of the default rules is the end of the line.
                Cancel();
                return;
            }

            if (!_context.TryReplaceItem(_item, promotedIndex, out BoardItem promotedItem))
            {
                Cancel();
                return;
            }

            _promotedItem = promotedItem;

            if (_awardScore)
            {
                _context.AddScore(_context.ScoreRule.GetScore(itemIndex));
            }

            Complete();
        }

        protected override void OnComplete()
        {
            _onPromoted?.Invoke(_promotedItem);
        }
    }
}
