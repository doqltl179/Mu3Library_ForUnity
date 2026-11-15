using System.Collections.Generic;
using Mu3Library.Game.WatermelonGame.Board.Command.Item;
using Mu3Library.Game.WatermelonGame.Board.Config;
using Mu3Library.Game.WatermelonGame.Board.Item;

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
                        if (TryCreateMergingCommand(item01, item02))
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

        /// <summary>
        /// Puts a pair the board found into a merge command.
        /// <br/> Touching is the board's own condition; the merge itself only asks for two items of
        /// <br/> one catalog entry, which is why a project can merge a pair that never met.
        /// </summary>
        protected bool TryCreateMergingCommand(BoardItem item01, BoardItem item02)
        {
            if (item01 == null ||
                item02 == null ||
                item01.Index < 0 ||
                item01.Index >= BoardItemsConfig.FruitItemCount ||
                !item01.CanMerge(item02))
            {
                return false;
            }

            MergingCommand command = CreateMergingCommand(item01, item02);
            if (command == null)
            {
                return false;
            }

            // The items stay in '_createdItems' until the command runs, so that 'PoolItemAll'
            // can still collect them when the board is prepared again.
            if (!command.IsValid || !command.HasMergeReservation || !EnqueueCommand(command))
            {
                command.Dispose();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Builds the merge for a pair the board found.
        /// <br/> Return a <see cref="MergingCommand"/> subclass here to give the whole board a merge
        /// <br/> that scores, grows, or sounds differently.
        /// </summary>
        protected virtual MergingCommand CreateMergingCommand(BoardItem item01, BoardItem item02)
            => new MergingCommand(CommandContext, item01, item02);
    }
}
