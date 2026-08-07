using System.Collections.Generic;
using Mu3Library.Game.WatermelonGame.Board.Config;
using Mu3Library.Game.WatermelonGame.Board.Item;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board
{
    public partial class BoardController
    {
        protected BoardConfig _boardConfig;

        /// <summary>
        /// Applies a complete board configuration to the board and all currently active items.
        /// The configuration is validated before any visible or physical state is changed.
        /// </summary>
        public virtual void SetBoardConfig(BoardConfig config)
        {
            if (!CanApplyBoardConfig(config))
            {
                return;
            }

            BoardConfig previousConfig = _boardConfig;
            _boardConfig = config;

            _boardArea.SetBoardImage(config.BoardSprite);
            _boardArea.SetBoardLocalItemOutLine(config.BoardLineSprite);
            _boardArea.SetBoardSpawnImage(config.BoardSpawnSprite);
            _boardArea.SetBoardSpawnGuideLine(config.BoardSpawnGuideLineSprite);

            for (int index = 0; index < _createdItems.Count; index++)
            {
                ApplyBoardConfigToItem(_createdItems[index], previousConfig, config);
            }

            ApplyBoardConfigToItem(_holdingItem, previousConfig, config);

            if (_holdingItem != null)
            {
                SetHoldingItemNormalizedX(_holdingNormalizedX);
            }
        }

        public void SetBoareConfig(BoardConfig config)
            => SetBoardConfig(config);

        private bool CanApplyBoardConfig(BoardConfig config)
        {
            if (_boardArea == null)
            {
                Debug.LogError("Board configuration cannot be applied without a BoardArea.", this);
                return false;
            }

            if (!IsCompleteBoardConfig(config))
            {
                return false;
            }

            return CanRemapItems(config, _createdItems) && CanRemapItem(config, _holdingItem);
        }

        private bool IsCompleteBoardConfig(BoardConfig config)
        {
            if (config == null)
            {
                Debug.LogError("Board configuration is required.", this);
                return false;
            }

            BoardItemsConfig itemConfig = config.ItemConfig;
            if (itemConfig == null)
            {
                Debug.LogError("Board configuration requires an item catalog.", config);
                return false;
            }

            for (int index = 0; index < BoardItemsConfig.FruitItemCount; index++)
            {
                if (!itemConfig.TryGetInfoByIndex(index, out BoardItemInfo itemInfo) || itemInfo == null)
                {
                    Debug.LogError($"Board configuration is missing item info at index {index}.", config);
                    return false;
                }
            }

            return true;
        }

        private bool CanRemapItems(BoardConfig targetConfig, List<BoardItem> items)
        {
            for (int index = 0; index < items.Count; index++)
            {
                if (!CanRemapItem(targetConfig, items[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CanRemapItem(BoardConfig targetConfig, BoardItem item)
        {
            if (item == null)
            {
                return true;
            }

            if (!TryGetItemIndex(item, _boardConfig, out int itemIndex) ||
                !targetConfig.ItemConfig.TryGetInfoByIndex(itemIndex, out BoardItemInfo itemInfo) ||
                itemInfo == null)
            {
                Debug.LogError("Board configuration cannot remap an item currently on the board.", this);
                return false;
            }

            return true;
        }

        private static bool TryGetItemIndex(BoardItem item, BoardConfig sourceConfig, out int itemIndex)
        {
            itemIndex = item != null ? item.Index : -1;
            if (itemIndex >= 0)
            {
                return true;
            }

            if (item == null || sourceConfig == null || sourceConfig.ItemConfig == null)
            {
                return false;
            }

            itemIndex = sourceConfig.ItemConfig.GetIndex(item.Info);
            return itemIndex >= 0;
        }

        private void ApplyBoardConfigToItem(BoardItem item, BoardConfig sourceConfig, BoardConfig targetConfig)
        {
            if (item == null ||
                !TryGetItemIndex(item, sourceConfig, out int itemIndex) ||
                !targetConfig.ItemConfig.TryGetInfoByIndex(itemIndex, out BoardItemInfo itemInfo) ||
                itemInfo == null)
            {
                return;
            }

            item.Init(itemIndex, itemInfo);
            item.transform.localScale = Vector3.one * GetItemScale(itemIndex, itemInfo);
            ApplyItemPhysics(item, targetConfig);
            ApplyItemGravity(item);
        }

        private BoardItemInfo GetItemInfo(int index)
        {
            if (_boardConfig == null ||
                _boardConfig.ItemConfig == null ||
                !_boardConfig.ItemConfig.TryGetInfoByIndex(index, out BoardItemInfo itemInfo))
            {
                return null;
            }

            return itemInfo;
        }
    }
}
