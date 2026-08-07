using System;
using Mu3Library.Game.WatermelonGame.Board.Item;

namespace Mu3Library.Game.WatermelonGame.Board.Command.Merge
{
    public class MergingCommand : BoardCommand
    {
        private bool _isValid =>
            _item01 != null &&
            _item02 != null &&
            _item01.Info != null &&
            _item02.Info != null &&
            _item01.Index >= 0 &&
            _item01.Index == _item02.Index;
        public bool IsValid => _isValid;

        private readonly BoardItem _item01;
        private readonly BoardItem _item02;

        private bool _isDisposed;

        private readonly Action _onStart;
        private readonly Action _onComplete;



        public MergingCommand(BoardItem item01, BoardItem item02, Action onStart = null, Action onComplete = null)
        {
            _isRunning = false;
            _isCompleted = false;

            if (item01 != null)
            {
                item01.SetMergeState(true);
            }
            if (item02 != null)
            {
                item02.SetMergeState(true);
            }

            _item01 = item01;
            _item02 = item02;

            _onStart = onStart;
            _onComplete = onComplete;
        }

        public override void Run()
        {
            if (_isDisposed || _isRunning || _isCompleted)
            {
                return;
            }

            _isRunning = true;

            _onStart?.Invoke();

            Complete();
        }

        public override void Dispose()
        {
            _isDisposed = true;
        }

        private void Complete()
        {
            if (_isDisposed || _isCompleted)
            {
                return;
            }

            _isRunning = false;
            _isCompleted = true;

            _onComplete?.Invoke();
        }
    }
}
