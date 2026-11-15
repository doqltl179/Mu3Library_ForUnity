using System.Collections.Generic;
using UnityEngine;
using Mu3Library.Game.WatermelonGame.Board.Item;

namespace Mu3Library.Game.WatermelonGame.Board.Config
{
    [System.Serializable]
    public class BoardItemsConfig
    {
        public const int FruitItemCount = 11;

        private static readonly IReadOnlyList<BoardItemInfo> EmptyItemInfos = new BoardItemInfo[0];

        [SerializeField] protected List<BoardItemInfo> _itemInfos = CreateDefaultItemInfos();
        public IReadOnlyList<BoardItemInfo> ItemInfos => _itemInfos ?? EmptyItemInfos;



        #region Utility
        public void EnsureItemInfoCount()
        {
            if (_itemInfos == null)
            {
                _itemInfos = CreateDefaultItemInfos();
                return;
            }

            while (_itemInfos.Count < FruitItemCount)
            {
                _itemInfos.Add(null);
            }
        }

        public BoardItemInfo GetInfoByIndex(int index)
        {
            EnsureItemInfoCount();

            if (TryGetInfoByIndex(index, out BoardItemInfo itemInfo))
            {
                return itemInfo;
            }

            if (index < 0 || index >= _itemInfos.Count)
            {
                Debug.LogWarning($"Requested item info index is out of range. index: {index}");
            }

            return null;
        }

        /// <summary>
        /// True when every fruit entry, the first <see cref="FruitItemCount"/>, can make an item.
        /// <br/> See <see cref="IsComplete(List{int})"/> for what an entry needs.
        /// </summary>
        public bool IsComplete() => IsComplete(null);

        /// <summary>
        /// True when every fruit entry, the first <see cref="FruitItemCount"/>, can make an item:
        /// <br/> the entry is there and carries a sprite. An item is sized from its sprite, so an
        /// <br/> entry without one would make an item with no size and nothing to touch with, and
        /// <br/> the board refuses such a catalog instead.
        /// </summary>
        /// <param name="incompleteIndices">
        /// Cleared and filled with the indices of the fruit entries that cannot make an item,
        /// <br/> in ascending order. Null only asks.
        /// </param>
        public bool IsComplete(List<int> incompleteIndices)
        {
            incompleteIndices?.Clear();

            bool isComplete = true;
            for (int index = 0; index < FruitItemCount; index++)
            {
                if (TryGetInfoByIndex(index, out BoardItemInfo itemInfo) && itemInfo.Sprite != null)
                {
                    continue;
                }

                isComplete = false;

                if (incompleteIndices == null)
                {
                    break;
                }

                incompleteIndices.Add(index);
            }

            return isComplete;
        }

        internal bool TryGetInfoByIndex(int index, out BoardItemInfo itemInfo)
        {
            itemInfo = null;

            if (_itemInfos == null || index < 0 || index >= _itemInfos.Count)
            {
                return false;
            }

            itemInfo = _itemInfos[index];
            return itemInfo != null;
        }

        public int GetIndex(BoardItemInfo info)
        {
            EnsureItemInfoCount();

            if (info == null)
            {
                return -1;
            }

            for (int index = 0; index < _itemInfos.Count; index++)
            {
                if (ReferenceEquals(_itemInfos[index], info))
                {
                    return index;
                }
            }

            return -1;
        }

        private static List<BoardItemInfo> CreateDefaultItemInfos()
        {
            List<BoardItemInfo> itemInfos = new(FruitItemCount);
            for (int index = 0; index < FruitItemCount; index++)
            {
                itemInfos.Add(null);
            }

            return itemInfos;
        }
        #endregion
    }
}
