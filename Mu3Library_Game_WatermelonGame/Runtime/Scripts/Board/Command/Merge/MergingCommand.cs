using System;
using Mu3Library.Game.WatermelonGame.Board.Item;

namespace Mu3Library.Game.WatermelonGame.Board.Command.Merge
{
    public class MergingCommand : BoardCommand
    {
        private bool _hasValidItems =>
            _item01 != null &&
            _item02 != null &&
            _item01 != _item02 &&
            _item01.Info != null &&
            _item02.Info != null &&
            _item01.Index >= 0 &&
            _item01.Index == _item02.Index;
        public bool IsValid => _hasValidItems;
        internal bool HasActiveMergeReservation => HasMergeReservation();

        private readonly BoardItem _item01;
        private readonly BoardItem _item02;

        private bool _isDisposed;
        private bool _hasMergeReservation;

        private readonly Action _onStart;
        private readonly Action _onComplete;



        public MergingCommand(BoardItem item01, BoardItem item02, Action onStart = null, Action onComplete = null)
        {
            _item01 = item01;
            _item02 = item02;
            _onStart = onStart;
            _onComplete = onComplete;

            TryReserveMerge();
        }

        public override void Run()
        {
            if (_isDisposed || _isRunning || _isCompleted)
            {
                return;
            }

            if (!HasMergeReservation())
            {
                ReleaseMergeReservation();
                _isCompleted = true;
                return;
            }

            _isRunning = true;

            try
            {
                _onStart?.Invoke();
                Complete();
            }
            catch
            {
                if (!_isCompleted)
                {
                    _isRunning = false;
                    ReleaseMergeReservation();
                }

                throw;
            }
        }

        public override void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            if (!_isCompleted)
            {
                _isRunning = false;
                ReleaseMergeReservation();
            }
        }

        private void Complete()
        {
            if (_isDisposed || _isCompleted)
            {
                return;
            }

            _isRunning = false;
            _isCompleted = true;

            try
            {
                _onComplete?.Invoke();
            }
            finally
            {
                ReleaseMergeReservation();
            }
        }

        private bool TryReserveMerge()
        {
            if (!_hasValidItems || !_item01.TryReserveMerge(this))
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

        private bool HasMergeReservation()
        {
            return _hasMergeReservation &&
                _item01 != null &&
                _item02 != null &&
                _item01.IsMergeReservedBy(this) &&
                _item02.IsMergeReservedBy(this);
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
