using Mu3Library.Game.WatermelonGame.Board.Config;
using Mu3Library.Game.WatermelonGame.Board.Item;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board
{
    public partial class BoardController
    {
        private enum SessionState
        {
            Unprepared,
            Prepared,
            Running,
            Ended,
        }

        protected bool _isPrepared;
        public bool IsPrepared => _isPrepared;

        protected bool _isRunning;
        public bool IsRunning => _isRunning;

        protected bool _isEnded;
        public bool IsEnded => _isEnded;

        protected int _score;
        public int Score => _score;

        protected int _spawnCount;
        public int SpawnCount => _spawnCount;

        protected int _mergeCount;
        public int MergeCount => _mergeCount;

        #region Utility
        /// <summary>
        /// Adds to the board score and reports it through <see cref="OnScoreChanged"/>.
        /// <br/> Merges count themselves through here, and so does a command that awards points,
        /// <br/> which keeps every score change on one path.
        /// <br/> A negative amount takes points away but never pushes the score below zero.
        /// </summary>
        public virtual void AddScore(int amount)
        {
            if (amount == 0)
            {
                return;
            }

            _score = Mathf.Max(0, _score + amount);

            OnScoreChanged?.Invoke(_score);
        }

        /// <summary>
        /// Counts one merge. Every merge runs through here, the ones the board found by itself
        /// <br/> and the ones a command carried out.
        /// </summary>
        public virtual void CountMerge()
        {
            _mergeCount++;
        }

        public virtual void GameStart()
        {
            if (CurrentSessionState != SessionState.Prepared)
            {
                Debug.LogWarning("Board can only start from the prepared state.", this);
                return;
            }

            SetSessionState(SessionState.Running);

            Debug.Log("Game Started");

            PlayBoardSound(BoardSoundType.GameStart);
            StartBoardBgm();

            OnGameStarted?.Invoke();
        }

        public virtual void GameEnd()
        {
            if (CurrentSessionState == SessionState.Unprepared || CurrentSessionState == SessionState.Ended)
            {
                return;
            }

            SetSessionState(SessionState.Ended);

            // The player can still be holding an item when the board overflows.
            ReleaseHoldingItem();

            Debug.Log("Game Ended");
            Debug.Log($"Score: {_score}");

            // The playlist stops first, so the sound that closes the game is not buried under it.
            StopBoardBgm();
            PlayBoardSound(BoardSoundType.GameEnd);

            OnGameEnded?.Invoke();
        }

        public virtual void Prepare(BoardSnapshot snapshot = null)
        {
            if (!CanPrepare())
            {
                return;
            }

            _score = 0;
            _spawnCount = 0;
            _mergeCount = 0;
            _lastDropTime = float.NegativeInfinity;
            _holdingNormalizedX = 0.5f;

            SetSessionState(SessionState.Unprepared);

            StopBoardBgm();
            ResetMergeCombo();

            _restoredItemIndices.Clear();

            ClearAllCommands();
            ReleaseHoldingItem();
            PoolItemAll();

            _boardArea.CalculateBounds();

            if (snapshot != null)
            {
                RestoreSnapshot(snapshot);
            }

            // The preview runs one item ahead, so the first one is drawn before the game starts.
            // A restored board hands out the items it was saved with first, so the draws they
            // already took are counted here.
            _drawCount = _spawnCount + _restoredItemIndices.Count;
            SetNextItemIndex(DrawNextItemIndex());

            Debug.Log("Game Prepared");

            SetSessionState(SessionState.Prepared);

            OnBoardPrepared?.Invoke();
        }

        private void SetSessionState(SessionState state)
        {
            _isPrepared = state != SessionState.Unprepared;
            _isRunning = state == SessionState.Running;
            _isEnded = state == SessionState.Ended;
        }

        private SessionState CurrentSessionState =>
            _isEnded
                ? SessionState.Ended
                : _isRunning
                    ? SessionState.Running
                    : _isPrepared
                        ? SessionState.Prepared
                        : SessionState.Unprepared;

        private bool CanPrepare()
        {
            if (_boardArea == null)
            {
                Debug.LogError("Board cannot be prepared without a BoardArea.", this);
                return false;
            }

            if (_itemResource == null)
            {
                Debug.LogError("Board cannot be prepared without a board item resource.", this);
                return false;
            }

            return IsCompleteBoardConfig(_boardConfig);
        }

        protected bool CheckGameEnd()
        {
            if (_boardArea == null)
            {
                return false;
            }

            Rect boardRect = _boardArea.LocalRect;
            if (boardRect.width <= Mathf.Epsilon || boardRect.height <= Mathf.Epsilon)
            {
                return false;
            }

            // An item that is stacked above the top edge of the board area has left the board.
            // The displayed item-out line is only a guide and never decides the game end.
            float itemOutPosY = boardRect.yMax;

            // Items resting on the floor or on other items are stacked, the side walls do not count.
            foreach (var item in _createdItems)
            {
                if (item == null || item == _holdingItem)
                {
                    continue;
                }

                if (item.IsStackedOverLine(itemOutPosY))
                {
                    return true;
                }
            }

            return false;
        }
        #endregion
    }
}
