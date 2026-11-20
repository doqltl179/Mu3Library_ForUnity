using Mu3Library.Game.WatermelonGame.Board.Item;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board
{
    /// <summary>
    /// What a single merge did, handed to <see cref="BoardController.OnItemMerged"/>.
    /// <br/> It carries what a project needs to answer a merge without reaching into the board:
    /// <br/> the entry that merged, the entry it became, where it happened and what it paid out.
    /// </summary>
    public readonly struct BoardMergeInfo
    {
        /// <summary>
        /// The catalog index both merged items carried, -1 when the merge was counted without
        /// <br/> telling what it merged.
        /// </summary>
        public readonly int ItemIndex;

        /// <summary>
        /// The catalog index of the item the pair became, -1 when the pair left nothing behind,
        /// <br/> which is what the last entry of the catalog does.
        /// </summary>
        public readonly int MergedItemIndex;

        /// <summary>
        /// The item the pair became, null when the pair left nothing behind.
        /// <br/> It is already on the board, so it can be read for its place or pushed around
        /// <br/> right away.
        /// </summary>
        public readonly BoardItem MergedItem;

        /// <summary>
        /// Where the pair met, as a fraction of the board area, which is the place a merge effect
        /// <br/> or a score popup belongs to.
        /// <br/> <see cref="BoardArea.BoardLocalNormalizedPositionToLocal"/> turns it into a position.
        /// </summary>
        public readonly Vector2 BoardNormalizedPosition;

        /// <summary>
        /// The points the merge paid out. They are already counted into
        /// <see cref="BoardController.Score"/> when the merge is reported.
        /// </summary>
        public readonly int Score;

        /// <summary>
        /// True while the merge was reported with the entry it merged, which is what every merge
        /// <br/> the board and <see cref="Command.Item.MergingCommand"/> carry out does.
        /// </summary>
        public bool IsValid => ItemIndex >= 0;

        public BoardMergeInfo(
            int itemIndex,
            int mergedItemIndex,
            BoardItem mergedItem,
            Vector2 boardNormalizedPosition,
            int score)
        {
            ItemIndex = itemIndex;
            MergedItemIndex = mergedItemIndex;
            MergedItem = mergedItem;
            BoardNormalizedPosition = boardNormalizedPosition;
            Score = score;
        }

        /// <summary>
        /// A merge that was counted without telling what it merged, which is what
        /// <see cref="BoardController.CountMerge()"/> reports.
        /// </summary>
        public static BoardMergeInfo Unknown => new BoardMergeInfo(-1, -1, null, Vector2.zero, 0);
    }
}
