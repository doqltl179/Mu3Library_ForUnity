using System.Collections.Generic;
using Mu3Library.Game.WatermelonGame.Board.Config;
using Mu3Library.Game.WatermelonGame.Board.Item;
using Mu3Library.Game.WatermelonGame.Board.Item.Rule;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Command
{
    /// <summary>
    /// What a command is allowed to do to the board.
    /// <br/> <see cref="BoardController"/> is the one that implements it, and hands it out through
    /// <see cref="BoardController.CommandContext"/>. A command written outside this package takes
    /// <br/> the context in its constructor and reaches the board only through it, so it never needs
    /// <br/> the board internals and keeps working when they change.
    /// </summary>
    public interface IBoardCommandContext
    {
        /// <summary>
        /// The board rectangle, for the coordinate conversions a command places its work with.
        /// </summary>
        public BoardArea Area { get; }

        /// <summary>
        /// The configuration the board runs on, null while none is applied.
        /// </summary>
        public BoardConfig Config { get; }

        /// <summary>
        /// The scoring the board counts merges with, which a command that awards points shares
        /// <br/> so a project only has to override the rule once.
        /// </summary>
        public BoardItemScoreRule ScoreRule { get; }

        /// <summary>
        /// The items that are on the board right now, the held one excluded.
        /// <br/> It is the board's own list, so copy it before removing anything.
        /// </summary>
        public IReadOnlyList<BoardItem> Items { get; }

        /// <summary>
        /// The item the player is about to drop, null while there is none.
        /// </summary>
        public BoardItem HoldingItem { get; }

        /// <summary>
        /// How many item indices the board plays with. A valid item index runs from 0 to this
        /// <br/> count, exclusive.
        /// </summary>
        public int ItemIndexCount { get; }

        public bool IsPrepared { get; }

        public bool IsRunning { get; }

        public int Score { get; }

        /// <summary>
        /// The catalog entry of an item index, null when the index is not part of the
        /// <br/> configuration the board runs on.
        /// </summary>
        public BoardItemInfo GetItemInfo(int itemIndex);

        /// <summary>
        /// True while the item is one of the items on the board, which a command checks before it
        /// <br/> acts on an item it picked a frame earlier.
        /// </summary>
        public bool ContainsItem(BoardItem item);

        /// <summary>
        /// Puts an item on the board and lets it fall, the way a merge result appears.
        /// </summary>
        /// <param name="itemIndex">The catalog index of the item.</param>
        /// <param name="boardNormalizedPosition">Where it appears, as a fraction of the board area.</param>
        /// <param name="item">The item that was placed, null when none was.</param>
        public bool TrySpawnItem(int itemIndex, Vector2 boardNormalizedPosition, out BoardItem item);

        /// <summary>
        /// Swaps an item on the board for another catalog entry, keeping the place it took.
        /// <br/> An item that a merge already reserved is left alone.
        /// </summary>
        public bool TryReplaceItem(BoardItem item, int itemIndex, out BoardItem replacement);

        /// <summary>
        /// Takes an item off the board. An item that a merge already reserved is left alone.
        /// </summary>
        public bool RemoveItem(BoardItem item);

        /// <summary>
        /// Adds to the board score, which raises <see cref="BoardController.OnScoreChanged"/>.
        /// <br/> A negative amount never pushes the score below zero.
        /// </summary>
        public void AddScore(int amount);

        /// <summary>
        /// Counts one merge into <see cref="BoardController.MergeCount"/> without telling what it
        /// <br/> merged, so a merge a command carried out is counted like one the board found by
        /// <br/> itself.
        /// </summary>
        public void CountMerge();

        /// <summary>
        /// Counts one merge and reports what it did through
        /// <see cref="BoardController.OnItemMerged"/>, which is how a project outside the board
        /// <br/> answers a merge with an effect, a popup, or a counter.
        /// <br/> Prefer this one over <see cref="CountMerge()"/> whenever the merge is known.
        /// </summary>
        public void CountMerge(BoardMergeInfo mergeInfo);

        /// <summary>
        /// Plays one of the sounds the board configuration carries. A moment without a clip
        /// <br/> stays silent.
        /// </summary>
        public void PlayBoardSound(BoardSoundType soundType);

        /// <summary>
        /// Plays the merge sound of the next combo step, so a command that merges items itself
        /// <br/> sounds like the board does.
        /// </summary>
        public void PlayItemMergeSound();

        /// <summary>
        /// Hands another command to the board, which is how one command chains into the next.
        /// </summary>
        public bool EnqueueCommand(IBoardCommand command);
    }
}
