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
        /// The configuration the board runs on, null until one was applied.
        /// </summary>
        public BoardConfig Config => _boardConfig;

        /// <summary>
        /// Reused by <see cref="IsCompleteBoardConfig"/> to name the entries a configuration lacks.
        /// </summary>
        private readonly List<int> _incompleteItemIndices = new();

        /// <summary>
        /// Where every board item sat as a fraction of the board, read right before a configuration
        /// <br/> refits the board, in the order of <see cref="_createdItems"/>. A null item is kept as
        /// <br/> NaN, and an item above the top edge reads above 1.
        /// </summary>
        private readonly List<Vector2> _itemBoardPositions = new();

        /// <summary>
        /// The board rectangle <see cref="_itemBoardPositions"/> was measured against.
        /// </summary>
        private Rect _itemBoardPositionsRect;

        /// <summary>
        /// Applies a complete board configuration to the board and all currently active items.
        /// The configuration is validated before any visible or physical state is changed.
        /// <br/> A board sprite of another size refits the board, and every item keeps the place it
        /// <br/> had as a fraction of the board, so the pile stays between the walls.
        /// </summary>
        public virtual void SetBoardConfig(BoardConfig config)
        {
            if (!CanApplyBoardConfig(config))
            {
                return;
            }

            BoardConfig previousConfig = _boardConfig;
            _boardConfig = config;

            // The items are children of the board area. Fitting a sprite of another size scales
            // the board transform, which carries them along while their local places stay the
            // same, so their places are read against the board before it is refitted.
            ReadItemBoardPositions();

            _boardArea.SetBoardImage(config.BoardSprite);
            _boardArea.SetBoardLocalItemOutLine(config.BoardLineSprite);
            _boardArea.SetBoardSpawnImage(config.BoardSpawnSprite);
            _boardArea.SetBoardSpawnGuideLine(config.BoardSpawnGuideLineSprite);

            for (int index = 0; index < _createdItems.Count; index++)
            {
                ApplyBoardConfigToItem(_createdItems[index], previousConfig, config);
            }

            PlaceItemsOnBoardPositions();

            ApplyBoardConfigToItem(_holdingItem, previousConfig, config);

            if (_holdingItem != null)
            {
                SetHoldingItemNormalizedX(_holdingNormalizedX);
            }

            OnBoardConfigChanged?.Invoke(config);
        }

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

            // An entry without a sprite would make an item with no size, so the configuration is
            // refused here rather than on the first item that draws the entry.
            if (!itemConfig.IsComplete(_incompleteItemIndices))
            {
                Debug.LogError(
                    $"Board configuration '{config.name}' cannot make the items at index {string.Join(", ", _incompleteItemIndices)}: " +
                    $"each of the first {BoardItemsConfig.FruitItemCount} item entries needs a sprite.",
                    config);
                return false;
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

        private void ReadItemBoardPositions()
        {
            _itemBoardPositions.Clear();
            _itemBoardPositionsRect = _boardArea.LocalRect;

            // Without the board rectangle there is no place to keep, and the items stay put.
            if (!HasArea(_itemBoardPositionsRect))
            {
                return;
            }

            for (int index = 0; index < _createdItems.Count; index++)
            {
                BoardItem item = _createdItems[index];

                // The board conversions clamp into the board, which would pull an item that is
                // stacked above the top edge back down onto the one below it, so the place is
                // measured against the rectangle directly.
                _itemBoardPositions.Add(item != null
                    ? NormalizeInRect(_itemBoardPositionsRect, item.transform.localPosition)
                    : new Vector2(float.NaN, float.NaN));
            }
        }

        private void PlaceItemsOnBoardPositions()
        {
            Rect boardRect = _boardArea.LocalRect;

            // A sprite of the same size refits the board onto the same rectangle, which leaves
            // every item where it is.
            int itemCount = Mathf.Min(_createdItems.Count, _itemBoardPositions.Count);
            if (!HasArea(boardRect) || boardRect == _itemBoardPositionsRect)
            {
                itemCount = 0;
            }

            for (int index = 0; index < itemCount; index++)
            {
                BoardItem item = _createdItems[index];
                Vector2 boardNormalizedPosition = _itemBoardPositions[index];
                if (item == null || float.IsNaN(boardNormalizedPosition.x))
                {
                    continue;
                }

                Transform itemTransform = item.transform;
                itemTransform.localPosition = new Vector3(
                    boardRect.xMin + boardNormalizedPosition.x * boardRect.width,
                    boardRect.yMin + boardNormalizedPosition.y * boardRect.height,
                    itemTransform.localPosition.z);
            }

            _itemBoardPositions.Clear();
        }

        private static bool HasArea(Rect rect)
            => rect.width > Mathf.Epsilon && rect.height > Mathf.Epsilon;

        private static Vector2 NormalizeInRect(Rect rect, Vector3 localPosition)
            => new Vector2(
                (localPosition.x - rect.xMin) / rect.width,
                (localPosition.y - rect.yMin) / rect.height);

        /// <summary>
        /// The catalog entry of an item index, null when the configuration the board runs on
        /// <br/> does not carry it.
        /// </summary>
        public BoardItemInfo GetItemInfo(int index)
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
