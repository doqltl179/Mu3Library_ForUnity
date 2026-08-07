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
