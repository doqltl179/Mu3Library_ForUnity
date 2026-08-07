using Mu3Library.Game.WatermelonGame.Board.Item;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board
{
    public partial class BoardController
    {
        #region Snapshot
        /// <summary>
        /// Writes the board as it stands into a snapshot, which <see cref="Prepare"/> brings back.
        /// <br/> Items that are about to merge are saved as the two items they still are, so the
        /// <br/> restored board merges them again and scores them then.
        /// </summary>
        public virtual BoardSnapshot ExportSnapshot()
        {
            BoardSnapshot snapshot = new BoardSnapshot
            {
                Score = _score,
                SpawnCount = _spawnCount,
                MergeCount = _mergeCount,

                // The held item never reached the board, it travels as an index and is put
                // back into the player's hand.
                HoldingItemIndex = _holdingItem != null ? _holdingItem.Index : -1,
                NextItemIndex = _nextItemIndex,
            };

            foreach (var item in _createdItems)
            {
                if (!TryExportItemSnapshot(item, out BoardItemSnapshot itemSnapshot))
                {
                    continue;
                }

                snapshot.BoardItemsSnapshot.Add(itemSnapshot);
            }

            return snapshot;
        }

        /// <summary>
        /// Writes the board as it stands as JSON, ready to be stored or sent.
        /// </summary>
        public string ExportSnapshotJson(bool prettyPrint = false)
            => ExportSnapshot().ToJson(prettyPrint);

        /// <summary>
        /// Prepares the board on the snapshot written by <see cref="ExportSnapshotJson"/>.
        /// <br/> An unreadable text prepares an empty board.
        /// </summary>
        public void ImportSnapshotJson(string json)
            => Prepare(BoardSnapshot.FromJson(json));

        /// <summary>
        /// Puts a saved board back on the prepared board. Called by <see cref="Prepare"/> once the
        /// <br/> board has been cleared, so it only fills in what the snapshot holds.
        /// </summary>
        protected virtual void RestoreSnapshot(BoardSnapshot snapshot)
        {
            _score = snapshot.Score;
            _spawnCount = snapshot.SpawnCount;
            _mergeCount = snapshot.MergeCount;

            // The saved items are drawn again before the spawn rule takes over.
            if (GetItemInfo(snapshot.HoldingItemIndex) != null)
            {
                _restoredItemIndices.Enqueue(snapshot.HoldingItemIndex);
            }
            if (GetItemInfo(snapshot.NextItemIndex) != null)
            {
                _restoredItemIndices.Enqueue(snapshot.NextItemIndex);
            }

            if (snapshot.BoardItemsSnapshot == null)
            {
                return;
            }

            if (_boardConfig == null)
            {
                Debug.LogWarning("BoardConfig not found. The saved items cannot be restored.");
                return;
            }

            foreach (var itemSnapshot in snapshot.BoardItemsSnapshot)
            {
                if (itemSnapshot == null)
                {
                    continue;
                }

                int itemIndex = itemSnapshot.Index;
                BoardItemInfo info = GetItemInfo(itemIndex);
                if (info == null)
                {
                    continue;
                }

                var item = _pool.Dequeue(info);
                if (item == null)
                {
                    continue;
                }

                Vector2 normalizedPosition = ClampSnapshotPosition(itemSnapshot.NormalizedLocalPosition);
                item.transform.SetLocalPositionAndRotation(
                    _boardArea.BoardLocalNormalizedPositionToLocal(normalizedPosition),
                    Quaternion.Euler(0.0f, 0.0f, itemSnapshot.LocalRotation));

                DropItem(item);
            }
        }

        /// <summary>
        /// Writes a single board item into a snapshot, false when it does not belong on a saved board.
        /// </summary>
        protected virtual bool TryExportItemSnapshot(BoardItem item, out BoardItemSnapshot itemSnapshot)
        {
            itemSnapshot = null;

            // The held item is not on the board yet, it is saved as 'HoldingItemIndex' instead.
            if (item == null || item == _holdingItem || item.Index < 0)
            {
                return false;
            }

            // The position is saved against the board area, which follows the screen resolution,
            // so the item lands on the same spot of the board on every device.
            if (_boardArea == null ||
                !_boardArea.TryLocalToBoardLocalNormalizedPosition(item.transform.localPosition, out Vector2 normalizedPosition))
            {
                return false;
            }

            itemSnapshot = new BoardItemSnapshot
            {
                Index = item.Index,
                NormalizedLocalPosition = normalizedPosition,

                // The board is a 2D plane, so the item only ever turns around Z.
                LocalRotation = item.transform.localEulerAngles.z,
            };

            return true;
        }

        private static Vector2 ClampSnapshotPosition(Vector2 normalizedPosition)
        {
            return new Vector2(
                ClampSnapshotCoordinate(normalizedPosition.x),
                ClampSnapshotCoordinate(normalizedPosition.y));
        }

        private static float ClampSnapshotCoordinate(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0.5f;
            }

            return Mathf.Clamp01(value);
        }
        #endregion
    }
}
