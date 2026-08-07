using System.Collections.Generic;
using UnityEngine;
using Mu3Library.Game.WatermelonGame.Board.Item;

namespace Mu3Library.Game.WatermelonGame.Board.Config
{
    [System.Serializable]
    public class BoardItemsConfig
    {
        public const int FruitItemCount = 11;

        [SerializeField] protected List<BoardItemInfo> _itemInfos = CreateDefaultItemInfos();
        public IReadOnlyList<BoardItemInfo> ItemInfos
        {
            get
            {
                EnsureItemInfoCount();
                return _itemInfos;
            }
        }



        #region Utility
        public void EnsureItemInfoCount()
        {
            if (_itemInfos == null)
            {
                _itemInfos = CreateDefaultItemInfos();
                return;
            }

            if (_itemInfos.Count > FruitItemCount)
            {
                _itemInfos.RemoveRange(FruitItemCount, _itemInfos.Count - FruitItemCount);
            }

            while (_itemInfos.Count < FruitItemCount)
            {
                _itemInfos.Add(null);
            }
        }

        public BoardItemInfo GetInfoByIndex(int index)
        {
            EnsureItemInfoCount();

            if (index < 0 || index >= FruitItemCount)
            {
                Debug.LogWarning($"Requested item info index is out of range. index: {index}");
                return null;
            }

            return _itemInfos[index];
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
