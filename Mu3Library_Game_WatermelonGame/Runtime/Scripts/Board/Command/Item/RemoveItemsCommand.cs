using System;
using System.Collections.Generic;
using Mu3Library.Game.WatermelonGame.Board.Item;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Command.Item
{
    /// <summary>
    /// Takes items off the board, which is what a bomb, a hammer, or any other power-up that
    /// <br/> clears a stack is made of.
    /// <br/> The items are copied when the command is built, so the board list it selected from can
    /// <br/> keep changing until the command runs.
    /// <br/> An item a merge already reserved is left alone, and an item that is no longer on the
    /// <br/> board is skipped, so a command built one frame and run the next stays safe.
    /// </summary>
    public class RemoveItemsCommand : BoardCommand
    {
        private static readonly BoardItem[] EmptyItems = new BoardItem[0];

        private readonly IBoardCommandContext _context;
        private readonly BoardItem[] _items;

        protected readonly bool _awardScore;
        /// <summary>
        /// True while a removed item pays out the points a merge would have paid for it.
        /// </summary>
        public bool AwardScore => _awardScore;

        protected int _removedCount;
        /// <summary>
        /// How many items were actually taken off the board.
        /// </summary>
        public int RemovedCount => _removedCount;

        private readonly Action<int> _onComplete;



        /// <param name="context">The board, taken from <see cref="BoardController.CommandContext"/>.</param>
        /// <param name="item">The item to take off the board.</param>
        /// <param name="awardScore">True to pay out the points a merge of the item would have paid.</param>
        /// <param name="onComplete">Called with how many items were removed, never on a canceled command.</param>
        public RemoveItemsCommand(
            IBoardCommandContext context,
            BoardItem item,
            bool awardScore = false,
            Action<int> onComplete = null)
            : this(context, item != null ? new[] { item } : null, awardScore, onComplete)
        {
        }

        /// <param name="context">The board, taken from <see cref="BoardController.CommandContext"/>.</param>
        /// <param name="items">The items to take off the board.</param>
        /// <param name="awardScore">True to pay out the points a merge of each item would have paid.</param>
        /// <param name="onComplete">Called with how many items were removed, never on a canceled command.</param>
        public RemoveItemsCommand(
            IBoardCommandContext context,
            IEnumerable<BoardItem> items,
            bool awardScore = false,
            Action<int> onComplete = null)
        {
            _context = context;
            _items = items != null ? new List<BoardItem>(items).ToArray() : EmptyItems;
            _awardScore = awardScore;
            _onComplete = onComplete;
        }

        protected override void OnRun()
        {
            if (_context == null)
            {
                Debug.LogWarning("Items cannot be removed without a board.");
                Cancel();
                return;
            }

            for (int index = 0; index < _items.Length; index++)
            {
                BoardItem item = _items[index];
                if (item == null)
                {
                    continue;
                }

                // The index is read before the item is pooled, which resets it.
                int itemIndex = item.Index;

                if (!_context.RemoveItem(item))
                {
                    continue;
                }

                _removedCount++;

                if (!_awardScore || itemIndex < 0)
                {
                    continue;
                }

                _context.AddScore(_context.ScoreRule.GetScore(itemIndex));
            }

            Complete();
        }

        protected override void OnComplete()
        {
            _onComplete?.Invoke(_removedCount);
        }
    }
}
