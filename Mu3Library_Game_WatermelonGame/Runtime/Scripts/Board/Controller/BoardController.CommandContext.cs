using Mu3Library.Game.WatermelonGame.Board.Command;
using Mu3Library.Game.WatermelonGame.Board.Config;
using Mu3Library.Game.WatermelonGame.Board.Item;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board
{
    /// <summary>
    /// The board as a command sees it. Everything a command is allowed to do to the board is
    /// <br/> gathered here, so a command written outside this package never has to reach into the
    /// <br/> board itself.
    /// </summary>
    public partial class BoardController : IBoardCommandContext
    {
        /// <summary>
        /// The board handed to the commands. Pass it to a command's constructor:
        /// <br/> <c>controller.EnqueueCommand(new SpawnItemCommand(controller.CommandContext, 0, new Vector2(0.5f, 0.9f)));</c>
        /// </summary>
        public IBoardCommandContext CommandContext => this;

        /// <summary>
        /// The board rectangle the items live in.
        /// </summary>
        public BoardArea Area => _boardArea;

        /// <summary>
        /// How many item indices the board plays with, the eleven default fruits.
        /// <br/> Catalog entries after them are kept for a project's own rules and are never
        /// <br/> spawned or merged by the default ones.
        /// </summary>
        public int ItemIndexCount => BoardItemsConfig.FruitItemCount;

        /// <summary>
        /// True while the item is one of the items on the board, the held one excluded.
        /// </summary>
        public bool ContainsItem(BoardItem item)
            => item != null && _createdItems.Contains(item);

        /// <summary>
        /// Puts an item on the board and lets it fall, the way the result of a merge appears.
        /// <br/> It is not counted as a player drop, so the drop interval and
        /// <see cref="SpawnCount"/> are left alone.
        /// </summary>
        /// <param name="itemIndex">The catalog index of the item.</param>
        /// <param name="boardNormalizedPosition">Where it appears, as a fraction of the board area.</param>
        /// <param name="item">The item that was placed, null when none was.</param>
        public virtual bool TrySpawnItem(int itemIndex, Vector2 boardNormalizedPosition, out BoardItem item)
        {
            item = null;

            // The board rectangle is measured while the board is prepared, and without it the
            // item cannot be given a place to fall from.
            if (!_isPrepared || _boardArea == null)
            {
                Debug.LogWarning("An item can only be spawned on a prepared board.", this);
                return false;
            }

            BoardItemInfo info = GetItemInfo(itemIndex);
            if (info == null)
            {
                Debug.LogError($"An item cannot be spawned for index {itemIndex}, the board configuration does not carry it.", this);
                return false;
            }

            BoardItem spawnedItem = _pool.Dequeue(info);
            if (spawnedItem == null)
            {
                return false;
            }

            Vector2 clampedPosition = _boardArea.ClampBoardNormalizedPosition(boardNormalizedPosition);
            spawnedItem.transform.localPosition = _boardArea.BoardLocalNormalizedPositionToLocal(clampedPosition);

            DropItem(spawnedItem);

            item = spawnedItem;
            return true;
        }

        /// <summary>
        /// Swaps an item on the board for another catalog entry, keeping the place it took.
        /// <br/> The old item goes back to the pool and the new one falls in from there.
        /// </summary>
        public virtual bool TryReplaceItem(BoardItem item, int itemIndex, out BoardItem replacement)
        {
            replacement = null;

            if (item == null || _boardArea == null)
            {
                return false;
            }

            if (GetItemInfo(itemIndex) == null)
            {
                Debug.LogError($"An item cannot be replaced with index {itemIndex}, the board configuration does not carry it.", this);
                return false;
            }

            // The place is read before the item leaves the board, returning it to the pool
            // resets its transform state.
            if (!_boardArea.TryLocalToBoardLocalNormalizedPosition(item.transform.localPosition, out Vector2 normalizedPosition))
            {
                return false;
            }

            if (!RemoveItem(item))
            {
                return false;
            }

            return TrySpawnItem(itemIndex, normalizedPosition, out replacement);
        }

        /// <summary>
        /// Takes an item off the board and returns it to the pool.
        /// <br/> An item a merge already reserved is left alone, its command owns it until it
        /// <br/> completes; the item the player holds is released instead.
        /// </summary>
        public virtual bool RemoveItem(BoardItem item)
        {
            if (item == null)
            {
                return false;
            }

            if (item == _holdingItem)
            {
                ReleaseHoldingItem();
                return true;
            }

            if (item.IsMerging || !_createdItems.Contains(item))
            {
                return false;
            }

            PoolItem(item);
            return true;
        }

        // The board plays its own sounds through protected members, which cannot serve an
        // interface, so the context reaches them through these.
        void IBoardCommandContext.PlayBoardSound(BoardSoundType soundType)
            => PlayBoardSound(soundType);

        void IBoardCommandContext.PlayItemMergeSound()
            => PlayItemMergeSound();
    }
}
