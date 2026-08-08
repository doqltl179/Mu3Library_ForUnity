using System;
using Mu3Library.Game.WatermelonGame.Board.Item;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Command.Item
{
    /// <summary>
    /// Merges two items that carry the same catalog entry: both leave the board, the points are
    /// <br/> paid, the next entry falls in where they met, and the merge sound plays on the running
    /// <br/> combo.
    /// <br/> The board builds one for every touching pair it finds, and a project builds one for a
    /// <br/> pair it picked itself, a magnet power-up pulling together two items that never touched.
    /// <br/> Only the contact is the board's condition, the command itself asks for nothing but the
    /// <br/> same catalog index.
    /// <br/> The pair is reserved the moment the command is built, so nothing else can claim it
    /// <br/> while the command waits its turn, and the reservation is handed back however it ends.
    /// <br/> Override the steps below for a merge that scores, grows, or sounds differently, and
    /// <br/> hand the subclass to the board through
    /// <see cref="BoardController.CreateMergingCommand(BoardItem, BoardItem)"/>.
    /// </summary>
    public class MergingCommand : BoardCommand
    {
        private readonly IBoardCommandContext _context;
        private readonly BoardItem _item01;
        private readonly BoardItem _item02;

        protected readonly int _itemIndex = -1;
        /// <summary>
        /// The catalog index both items carry, -1 when they are not a pair.
        /// <br/> It is read when the command is built, the items lose it once they are pooled.
        /// </summary>
        public int ItemIndex => _itemIndex;

        protected BoardItem _mergedItem;
        /// <summary>
        /// The item the pair became, null until the command completed and when the pair carried
        /// <br/> the last entry of the catalog, which leaves nothing behind.
        /// </summary>
        public BoardItem MergedItem => _mergedItem;

        /// <summary>
        /// True while the two are a pair that can be merged at all: two different items of one
        /// <br/> board, carrying the same catalog entry.
        /// </summary>
        public bool IsValid =>
            _context != null &&
            _item01 != null &&
            _item02 != null &&
            _item01 != _item02 &&
            _item01.Info != null &&
            _item02.Info != null &&
            _item01.Index >= 0 &&
            _item01.Index == _item02.Index;

        /// <summary>
        /// True while this command is the one that holds both items, which is what keeps a second
        /// <br/> command from merging one of them into another pair.
        /// <br/> A command that never got the pair cancels itself instead of running.
        /// </summary>
        public bool HasMergeReservation =>
            _hasMergeReservation &&
            _item01 != null &&
            _item02 != null &&
            _item01.IsMergeReservedBy(this) &&
            _item02.IsMergeReservedBy(this);

        private bool _hasMergeReservation;

        private readonly Action<BoardItem> _onMerged;



        /// <param name="context">The board, taken from <see cref="BoardController.CommandContext"/>.</param>
        /// <param name="item01">One of the two items, which have to carry the same catalog index.</param>
        /// <param name="item02">The other item.</param>
        /// <param name="onMerged">Called with the item the pair became, null at the end of the catalog and never on a canceled command.</param>
        public MergingCommand(
            IBoardCommandContext context,
            BoardItem item01,
            BoardItem item02,
            Action<BoardItem> onMerged = null)
        {
            _context = context;
            _item01 = item01;
            _item02 = item02;
            _onMerged = onMerged;

            _itemIndex = IsValid ? item01.Index : -1;

            TryReserveMerge();
        }

        protected override void OnRun()
        {
            // The pair can be taken apart between the moment it was put together and the moment
            // the merge gets to run.
            if (!IsValid || !HasMergeReservation)
            {
                Cancel();
                return;
            }

            if (!TryGetMergedPosition(out Vector2 boardNormalizedPosition) ||
                !_context.ContainsItem(_item01) ||
                !_context.ContainsItem(_item02))
            {
                Cancel();
                return;
            }

            // The reservation only had to hold the pair together until here. The board refuses to
            // remove a reserved item, and from this point the pair is leaving anyway.
            ReleaseMergeReservation();

            _context.RemoveItem(_item01);
            _context.RemoveItem(_item02);

            _context.AddScore(GetMergeScore(_itemIndex));

            _mergedItem = SpawnMergedItem(GetMergedIndex(_itemIndex), boardNormalizedPosition);

            _context.CountMerge();

            PlayMergeSound();

            Complete();
        }

        protected override void OnComplete()
        {
            _onMerged?.Invoke(_mergedItem);
        }

        protected override void OnCancel()
        {
            ReleaseMergeReservation();
        }

        protected override void OnDispose()
        {
            ReleaseMergeReservation();
        }

        /// <summary>
        /// Where the merged item appears, the middle between the two by default.
        /// </summary>
        protected virtual bool TryGetMergedPosition(out Vector2 boardNormalizedPosition)
        {
            boardNormalizedPosition = Vector2.zero;

            BoardArea area = _context.Area;
            if (area == null)
            {
                return false;
            }

            Vector3 localMiddlePosition = (_item01.transform.localPosition + _item02.transform.localPosition) * 0.5f;

            return area.TryLocalToBoardLocalNormalizedPosition(localMiddlePosition, out boardNormalizedPosition);
        }

        /// <summary>
        /// The catalog entry the pair becomes, the one right after it by default.
        /// </summary>
        protected virtual int GetMergedIndex(int itemIndex)
            => itemIndex + 1;

        /// <summary>
        /// The points the pair pays out, taken from the board scoring rule by default.
        /// </summary>
        protected virtual int GetMergeScore(int itemIndex)
            => _context.ScoreRule.GetScore(itemIndex);

        /// <summary>
        /// Puts the merged item on the board, null when the pair leaves nothing behind.
        /// </summary>
        protected virtual BoardItem SpawnMergedItem(int mergedIndex, Vector2 boardNormalizedPosition)
        {
            // The last entry of the catalog has nothing above it, so the pair only pays out.
            if (mergedIndex < 0 || mergedIndex >= _context.ItemIndexCount)
            {
                return null;
            }

            return _context.TrySpawnItem(mergedIndex, boardNormalizedPosition, out BoardItem mergedItem)
                ? mergedItem
                : null;
        }

        /// <summary>
        /// The sound the merge makes, the next step of the board merge combo by default.
        /// </summary>
        protected virtual void PlayMergeSound()
        {
            _context.PlayItemMergeSound();
        }

        private bool TryReserveMerge()
        {
            if (!IsValid || !_item01.TryReserveMerge(this))
            {
                return false;
            }

            if (_item02.TryReserveMerge(this))
            {
                _hasMergeReservation = true;
                return true;
            }

            _item01.ReleaseMergeReservation(this);
            return false;
        }

        private void ReleaseMergeReservation()
        {
            if (!_hasMergeReservation)
            {
                return;
            }

            if (_item01 != null)
            {
                _item01.ReleaseMergeReservation(this);
            }

            if (_item02 != null)
            {
                _item02.ReleaseMergeReservation(this);
            }

            _hasMergeReservation = false;
        }
    }
}
