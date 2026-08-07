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
        [Tooltip("The smallest item diameter as a fraction of the board area's local width.")]
        [SerializeField, Range(0.01f, 0.5f)] protected float _smallestItemBoardWidthRatio = BoardItemScaleRule.DefaultSmallestBoardWidthRatio;
        [Tooltip("The largest item diameter as a fraction of the board area's local width.\nEvery item in between is spread linearly over this range.")]
        [SerializeField, Range(0.05f, 1.0f)] protected float _largestItemBoardWidthRatio = BoardItemScaleRule.DefaultLargestBoardWidthRatio;

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
        /// Raised with <see cref="NextItemIndex"/> whenever the preview changes, which happens
        /// when the board is prepared and every time an item is taken into the player's hand.
        /// </summary>
        public event Action<int> OnNextItemChanged;



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
