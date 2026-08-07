using System.Collections.Generic;
using Mu3Library.Game.WatermelonGame.Board.Command.Merge;
using Mu3Library.Game.WatermelonGame.Board.Config;
using Mu3Library.Game.WatermelonGame.Board.Item;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board
{
    public partial class BoardController
    {
        // Groups are retained between frames so matching equal items does not allocate or compare
        // every item against unrelated item types.
        private readonly Dictionary<int, List<BoardItem>> _mergeItemsByIndex = new();
        private readonly List<int> _activeMergeIndices = new();

        protected void CreateMergingCommands()
        {
            BuildMergeGroups();

            for (int groupIndex = 0; groupIndex < _activeMergeIndices.Count; groupIndex++)
            {
                List<BoardItem> items = _mergeItemsByIndex[_activeMergeIndices[groupIndex]];

                for (int firstIndex = 0; firstIndex < items.Count - 1; firstIndex++)
                {
                    BoardItem item01 = items[firstIndex];
                    if (item01 == null || item01.IsMerging)
                    {
                        continue;
                    }

                    for (int secondIndex = firstIndex + 1; secondIndex < items.Count; secondIndex++)
                    {
                        BoardItem item02 = items[secondIndex];
                        if (item02 == null || item02.IsMerging)
                        {
                            continue;
                        }

                        // item01 is reserved from now on, so it cannot pair with anything else.
                        if (CreateMergingCommand(item01, item02))
                        {
                            break;
                        }
                    }
                }
            }

            ClearMergeGroups();
        }

        private void BuildMergeGroups()
        {
            ClearMergeGroups();

            for (int itemListIndex = 0; itemListIndex < _createdItems.Count; itemListIndex++)
            {
                BoardItem item = _createdItems[itemListIndex];
                if (item == null ||
                    item.IsMerging ||
                    item.Index < 0 ||
                    item.Index >= BoardItemsConfig.FruitItemCount)
                {
                    continue;
                }

                int itemIndex = item.Index;
                if (!_mergeItemsByIndex.TryGetValue(itemIndex, out List<BoardItem> items))
                {
                    items = new List<BoardItem>();
                    _mergeItemsByIndex.Add(itemIndex, items);
                }

                if (items.Count == 0)
                {
                    _activeMergeIndices.Add(itemIndex);
                }

                items.Add(item);
            }
        }

        private void ClearMergeGroups()
        {
            for (int groupIndex = 0; groupIndex < _activeMergeIndices.Count; groupIndex++)
            {
                _mergeItemsByIndex[_activeMergeIndices[groupIndex]].Clear();
            }

            _activeMergeIndices.Clear();
        }

        protected bool CreateMergingCommand(BoardItem item01, BoardItem item02)
        {
            if (item01 == null ||
                item02 == null ||
                item01.Index < 0 ||
                item01.Index >= BoardItemsConfig.FruitItemCount ||
                !item01.CanMerge(item02))
            {
                return false;
            }

            int itemIndex = item01.Index;
            Vector3 localMiddlePos = (item01.transform.localPosition + item02.transform.localPosition) * 0.5f;

            void OnStart()
            {
                item01.gameObject.SetActive(false);
                item02.gameObject.SetActive(false);
            }
            void OnComplete()
            {
                _score += _scoreRule.GetScore(itemIndex);
                OnScoreChanged?.Invoke(_score);

                PoolItem(item01);
                PoolItem(item02);

                int nextIndex = itemIndex + 1;
                BoardItemInfo nextInfo = nextIndex < BoardItemsConfig.FruitItemCount
                    ? GetItemInfo(nextIndex)
                    : null;

                if (nextInfo != null)
                {
                    BoardItem nextItem = _pool.Dequeue(nextInfo);
                    if (nextItem != null)
                    {
                        nextItem.transform.localPosition = localMiddlePos;

                        DropItem(nextItem);
                    }
                }

                _mergeCount++;
            }

            // The items stay in '_createdItems' until the command completes, so that
            // 'PoolItemAll' can still collect them when the board is prepared again.
            MergingCommand command = new MergingCommand(item01, item02, OnStart, OnComplete);
            if (!command.IsValid || !command.HasActiveMergeReservation)
            {
                command.Dispose();
                return false;
            }

            _commands.Add(command);

            return true;
        }
    }
}
