using System;
using Mu3Library.Game.WatermelonGame.Board.Item;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Command.Item
{
    /// <summary>
    /// Drops an item onto the board without the player having to hold it, which is what a bonus
    /// <br/> item or a board that starts on a prepared stack is made of.
    /// <br/> It is not a player drop, so the drop interval and <see cref="BoardController.SpawnCount"/>
    /// <br/> are left alone.
    /// <br/> The command is canceled when the board cannot place the item, so a
    /// <see cref="Flow.SequenceCommand"/> around it does not carry on as if it had.
    /// </summary>
    public class SpawnItemCommand : BoardCommand
    {
        private readonly IBoardCommandContext _context;

        protected readonly int _itemIndex;
        public int ItemIndex => _itemIndex;

        protected readonly Vector2 _boardNormalizedPosition;
        /// <summary>
        /// Where the item appears, as a fraction of the board area.
        /// </summary>
        public Vector2 BoardNormalizedPosition => _boardNormalizedPosition;

        protected BoardItem _spawnedItem;
        /// <summary>
        /// The item that was placed, null until the command completed.
        /// </summary>
        public BoardItem SpawnedItem => _spawnedItem;

        private readonly Action<BoardItem> _onSpawned;



        /// <param name="context">The board, taken from <see cref="BoardController.CommandContext"/>.</param>
        /// <param name="itemIndex">The catalog index of the item.</param>
        /// <param name="boardNormalizedPosition">Where it appears, as a fraction of the board area.</param>
        /// <param name="onSpawned">Called with the item that was placed, never on a canceled command.</param>
        public SpawnItemCommand(
            IBoardCommandContext context,
            int itemIndex,
            Vector2 boardNormalizedPosition,
            Action<BoardItem> onSpawned = null)
        {
            _context = context;
            _itemIndex = itemIndex;
            _boardNormalizedPosition = boardNormalizedPosition;
            _onSpawned = onSpawned;
        }

        protected override void OnRun()
        {
            if (_context == null)
            {
                Debug.LogWarning("An item cannot be spawned without a board.");
                Cancel();
                return;
            }

            if (!_context.TrySpawnItem(_itemIndex, _boardNormalizedPosition, out BoardItem item))
            {
                Cancel();
                return;
            }

            _spawnedItem = item;

            Complete();
        }

        protected override void OnComplete()
        {
            _onSpawned?.Invoke(_spawnedItem);
        }
    }
}
