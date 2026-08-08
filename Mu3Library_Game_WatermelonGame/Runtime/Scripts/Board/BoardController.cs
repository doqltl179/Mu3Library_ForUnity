using System;
using Mu3Library.Attribute;
using Mu3Library.Game.WatermelonGame.Board.Config;
using Mu3Library.Game.WatermelonGame.Board.Item;
using Mu3Library.Game.WatermelonGame.Board.Item.Rule;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board
{
    public partial class BoardController : MonoBehaviour
    {
        [Title("Board")]
        [SerializeField] protected BoardArea _boardArea;

        [Title("Board Item")]
        [SerializeField] protected BoardItem _itemResource;

        [Title("Drop")]
        [Tooltip("Seconds the player has to wait between two items.")]
        [SerializeField, Min(0.0f)] protected float _dropInterval = 0.5f;
        [Tooltip("The downward speed a dropped item starts its fall with, as a fraction of the board area height per second.\nThe board area follows the screen resolution, so the drop stays the same on every device.")]
        [SerializeField, Min(0.0f)] protected float _dropSpeedBoardHeightRatio = 0.5f;
        [Tooltip("The downward acceleration of a falling item, as a fraction of the board area height per second squared.\nIt scales the item gravity with the board area, so the whole fall, and not only its start, stays the same on every device.\nThe project gravity is never modified.")]
        [SerializeField, Min(0.0f)] protected float _gravityBoardHeightRatio = 1.5f;

        protected BoardItemScaleRule _scaleRule = new BoardItemScaleRule();
        public BoardItemScaleRule ScaleRule
        {
            get => _scaleRule;
            set => _scaleRule = value ?? _scaleRule;
        }

        protected BoardItemSpawnRule _spawnRule = new BoardItemSpawnRule();
        public BoardItemSpawnRule SpawnRule
        {
            get => _spawnRule;
            set => _spawnRule = value ?? _spawnRule;
        }

        protected BoardItemScoreRule _scoreRule = new BoardItemScoreRule();
        public BoardItemScoreRule ScoreRule
        {
            get => _scoreRule;
            set => _scoreRule = value ?? _scoreRule;
        }

        private bool _isBoardAreaSubscribed;

        public event Action OnBoardPrepared;
        public event Action OnGameStarted;
        public event Action OnGameEnded;

        public event Action<int> OnScoreChanged;

        /// <summary>
        /// Raised with the points a single change paid out and the score they were added to.
        /// <br/> It is the change itself and not the running total, which is what a score popup
        /// <br/> shows; the score never goes below zero, so a negative amount can pay out less
        /// <br/> than it asked for, and a change that moves nothing is not reported.
        /// </summary>
        public event Action<int, int> OnScoreAdded;

        /// <summary>
        /// Raised with <see cref="NextItemIndex"/> whenever the preview changes, which happens
        /// when the board is prepared and every time an item is taken into the player's hand.
        /// </summary>
        public event Action<int> OnNextItemChanged;

        /// <summary>
        /// Raised with the configuration the board now runs on, once it has been applied to the
        /// <br/> board and to every item on it, so a preview drawn outside the board can pick up
        /// <br/> the sprites it changed.
        /// </summary>
        public event Action<BoardConfig> OnBoardConfigChanged;

        /// <summary>
        /// Raised with the item that is now in the player's hand, and with null when the hand is
        /// <br/> emptied, which happens when the item is dropped and when the board is prepared or
        /// <br/> ends the game while the player still holds one.
        /// </summary>
        public event Action<BoardItem> OnHoldingItemChanged;

        /// <summary>
        /// Raised with the place the held item was moved to, as a fraction of the board area width.
        /// <br/> It follows the player's drag, so it only reports a place that really changed.
        /// </summary>
        public event Action<float> OnHoldingItemMoved;

        /// <summary>
        /// Raised with the item the player just released. It is already falling, and
        /// <see cref="SpawnCount"/> and the drop interval have been counted.
        /// </summary>
        public event Action<BoardItem> OnItemDropped;

        /// <summary>
        /// Raised with an item that joined the board, whoever put it there: the player, a merge,
        /// <br/> a command, or a snapshot being restored.
        /// </summary>
        public event Action<BoardItem> OnItemAdded;

        /// <summary>
        /// Raised with an item that left the board and is on its way back to the item pool, so it
        /// <br/> still carries its catalog entry and its place while the event runs.
        /// <br/> <see cref="Prepare"/> clears the board through here as well.
        /// </summary>
        public event Action<BoardItem> OnItemRemoved;

        /// <summary>
        /// Raised with what a merge did, every merge the board found by itself and every merge a
        /// <br/> command carried out.
        /// </summary>
        public event Action<BoardMergeInfo> OnItemMerged;

        /// <summary>
        /// Raised with <see cref="MergeComboIndex"/> whenever the merge combo step changes, and
        /// <br/> with -1 when the combo is dropped.
        /// <br/> The combo is what the merge sounds climb, so a board configuration without sounds
        /// <br/> never reports one.
        /// </summary>
        public event Action<int> OnMergeComboChanged;


        protected virtual void OnEnable()
        {
            if (_boardArea == null || _isBoardAreaSubscribed)
            {
                if (_boardArea == null)
                {
                    Debug.LogError("BoardArea reference is required.", this);
                }

                return;
            }

            _boardArea.OnTouchBegan += OnDragStart;
            _boardArea.OnTouchMoved += OnDragging;
            _boardArea.OnTouchEnded += OnDragEnd;

            _isBoardAreaSubscribed = true;
        }

        protected virtual void OnDisable()
        {
            _boardArea?.SetBoardSpawnGuideLineVisible(false);

            if (_boardArea == null || !_isBoardAreaSubscribed)
            {
                return;
            }

            _boardArea.OnTouchBegan -= OnDragStart;
            _boardArea.OnTouchMoved -= OnDragging;
            _boardArea.OnTouchEnded -= OnDragEnd;

            _isBoardAreaSubscribed = false;
        }

        protected virtual void OnDestroy()
        {
            ClearAllCommands();

            StopBoardBgm();
            DisposeOwnedAudioManager();
        }

        protected virtual void Start()
        {
            if (_itemResource != null)
            {
                _itemResource.gameObject.SetActive(false);
            }
            else
            {
                Debug.LogError("Board item resource reference is required.", this);
            }
        }

        protected virtual void Update()
        {
            // The board's own audio manager is a plain class, so nothing else advances its
            // SFX pooling and its BGM playlist. It is driven even while the board is idle,
            // so a game-end sound is still cleaned up after the board stopped running.
            UpdateOwnedAudioManager();

            if (!_isRunning)
            {
                return;
            }

            UpdateCommands();
            CreateMergingCommands();
            UpdateHoldingItem();

            // The item dropped last is still settling, so the stack is given the drop interval
            // to slide into place before it is judged.
            if (!IsGameEndCheckPaused && CheckGameEnd())
            {
                GameEnd();
            }
        }
    }
}
