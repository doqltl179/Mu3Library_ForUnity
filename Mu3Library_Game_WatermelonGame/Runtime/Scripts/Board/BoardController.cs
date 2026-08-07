using System;
using System.Collections.Generic;
using Mu3Library.Attribute;
using Mu3Library.Game.WatermelonGame.Board.Command;
using Mu3Library.Game.WatermelonGame.Board.Command.Merge;
using Mu3Library.Game.WatermelonGame.Board.Config;
using Mu3Library.Game.WatermelonGame.Board.Item;
using Mu3Library.Game.WatermelonGame.Board.Item.Rule;
using Mu3Library.ObjectPool;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board
{
    public class BoardController : MonoBehaviour
    {
        [Title("Board")]
        [SerializeField] protected BoardArea _boardArea;

        [Title("Board Item")]
        [SerializeField] protected BoardItem _itemResource;
        [Tooltip("The smallest item width as a fraction of the board area's local width.")]
        [SerializeField, Range(0.05f, 0.5f)] protected float _smallestItemBoardWidthRatio = 0.05f;

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

        protected BoardConfig _boardConfig;

        protected readonly List<BoardItem> _createdItems = new();
        protected BoardItem _holdingItem;

        private GameObjectPool<BoardItem, BoardItemInfo> m_pool;
        protected GameObjectPool<BoardItem, BoardItemInfo> _pool => m_pool ??= new GameObjectPool<BoardItem, BoardItemInfo>(CreateBoardItem, InitBoardItem);

        protected readonly List<IBoardCommand> _commands = new();

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

        public event Action OnBoardPrepared;
        public event Action OnGameStarted;
        public event Action OnGameEnded;

        public event Action<int> OnScoreChanged;



        protected virtual void OnEnable()
        {
            _boardArea.OnTouchBegan += OnDragStart;
            _boardArea.OnTouchMoved += OnDragging;
            _boardArea.OnTouchEnded += OnDragEnd;
        }

        protected virtual void OnDisable()
        {
            _boardArea.OnTouchBegan -= OnDragStart;
            _boardArea.OnTouchMoved -= OnDragging;
            _boardArea.OnTouchEnded -= OnDragEnd;
        }

        protected virtual void OnDestroy()
        {
            ClearAllCommands();
        }

        protected virtual void Start()
        {
            _itemResource.gameObject.SetActive(false);
        }

        protected virtual void Update()
        {
            if (!_isRunning)
            {
                return;
            }

            UpdateCommands();
            CreateMergingCommands();

            if (CheckGameEnd())
            {
                GameEnd();
            }
        }

        #region Utility
        public virtual void GameStart()
        {
            if (!_isPrepared)
            {
                Debug.LogWarning("Board is not prepared. Please prepare board first.");
                return;
            }

            _isRunning = true;

            OnGameStarted?.Invoke();
        }

        public virtual void GameEnd()
        {
            if (_isEnded)
            {
                return;
            }

            _isRunning = false;
            _isEnded = true;

            // The player can still be holding an item when the board overflows.
            ReleaseHoldingItem();

            OnGameEnded?.Invoke();
        }

        public virtual void Prepare(BoardSnapshot snapshot = null)
        {
            _score = 0;
            _spawnCount = 0;
            _mergeCount = 0;

            _isPrepared = false;
            _isRunning = false;
            _isEnded = false;

            ClearAllCommands();
            ReleaseHoldingItem();
            PoolItemAll();

            _boardArea.CalculateBounds();

            if (snapshot != null)
            {
                _score = snapshot.Score;
                _spawnCount = snapshot.SpawnCount;
                _mergeCount = snapshot.MergeCount;

                foreach (var itemSnapshot in snapshot.BoardItemsSnapshot)
                {
                    int itemIndex = itemSnapshot.Index;
                    var info = _boardConfig.ItemConfig.GetInfoByIndex(itemIndex);
                    if (info == null)
                    {
                        continue;
                    }

                    var item = _pool.Dequeue(info);
                    if (item == null)
                    {
                        continue;
                    }

                    item.transform.localPosition = _boardArea.BoardLocalNormalizedPositionToLocal(itemSnapshot.NormalizedLocalPosition);

                    item.SetFallState(true);
                    item.BodyType = RigidbodyType2D.Dynamic;
                    item.ColliderEnabled = true;

                    AddCreatedItem(item);
                }
            }

            Debug.Log("Game Prepared");

            _isPrepared = true;

            OnBoardPrepared?.Invoke();
        }

        public void SetBoareConfig(BoardConfig config)
        {
            if (config == null)
            {
                return;
            }

            _boardArea.SetBoardImage(config.BoardSprite);
            _boardArea.SetBoardLocalItemOutLine(config.BoardLineSprite);

            foreach (var item in _createdItems)
            {
                if (item == null)
                {
                    continue;
                }

                int itemIndex = item.Index;
                if (itemIndex < 0 && _boardConfig != null)
                {
                    itemIndex = _boardConfig.ItemConfig.GetIndex(item.Info);
                }

                if (itemIndex < 0)
                {
                    continue;
                }

                // Change Skin(Sprite)
                var info = config.ItemConfig.GetInfoByIndex(itemIndex);
                item.Init(itemIndex, info);
            }

            _boardConfig = config;
        }
        #endregion

        #region Event
        public virtual void OnDragStart(Vector2 screenPos)
        {
            if (!_isRunning)
            {
                return;
            }

            if (_holdingItem != null)
            {
                Debug.LogWarning("Holding item already exist.");
            }
            else
            {
                int index = _spawnRule.GetNextIndex(_spawnCount);
                var info = _boardConfig.ItemConfig.GetInfoByIndex(index);
                if (info == null)
                {
                    return;
                }

                var item = _pool.Dequeue(info);
                if (item == null)
                {
                    return;
                }

                item.BodyType = RigidbodyType2D.Kinematic;
                item.ColliderEnabled = false;

                _holdingItem = item;
            }

            SetHoldingItemPosition(screenPos);
        }

        public virtual void OnDragging(Vector2 screenPos)
        {
            if (_holdingItem == null)
            {
                return;
            }

            SetHoldingItemPosition(screenPos);
        }

        public virtual void OnDragEnd(Vector2 screenPos)
        {
            if (_holdingItem == null)
            {
                return;
            }

            SetHoldingItemPosition(screenPos);

            var item = _holdingItem;
            _holdingItem = null;

            item.SetFallState(true);
            item.BodyType = RigidbodyType2D.Dynamic;
            item.ColliderEnabled = true;

            AddCreatedItem(item);

            _spawnCount++;
        }
        #endregion

        protected void UpdateCommands()
        {
            for (int i = 0; i < _commands.Count; i++)
            {
                var cmd = _commands[i];

                if (cmd == null)
                {
                    _commands.RemoveAt(i);
                    i--;
                }
                else if (cmd.IsCompleted)
                {
                    cmd.Dispose();
                    _commands.RemoveAt(i);
                    i--;
                }
                else if (!cmd.IsRunning)
                {
                    cmd.Run();
                }
            }
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

            float itemOutPosY = _boardArea.BoardLocalNormalizedPositionToLocal(
                new Vector2(0.0f, _boardArea.BoardLocalNormalizedItemOutPosY)).y;

            // Items resting on the floor or on other items are stacked, the side walls do not count.
            Collider2D floorCollider = _boardArea.BottomOutCollider;

            foreach (var item in _createdItems)
            {
                if (item == null || item == _holdingItem)
                {
                    continue;
                }

                if (item.IsStackedOverLine(itemOutPosY, floorCollider))
                {
                    return true;
                }
            }

            return false;
        }

        protected void CreateMergingCommands()
        {
            for (int i = 0; i < _createdItems.Count - 1; i++)
            {
                BoardItem item01 = _createdItems[i];
                if (item01 == null || item01.IsMerging)
                {
                    continue;
                }

                for (int j = i + 1; j < _createdItems.Count; j++)
                {
                    BoardItem item02 = _createdItems[j];
                    if (item02 == null || item02.IsMerging)
                    {
                        continue;
                    }

                    // item01 is merging from now on, so it cannot pair with anything else.
                    if (CreateMergingCommand(item01, item02))
                    {
                        break;
                    }
                }
            }
        }

        protected bool CreateMergingCommand(BoardItem item01, BoardItem item02)
        {
            if (item01 == null || item02 == null || !item01.CanMerge(item02))
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
                BoardItemInfo nextInfo = _boardConfig != null
                    ? _boardConfig.ItemConfig.GetInfoByIndex(nextIndex)
                    : null;

                if (nextInfo != null)
                {
                    BoardItem nextItem = _pool.Dequeue(nextInfo);
                    if (nextItem != null)
                    {
                        nextItem.transform.localPosition = localMiddlePos;

                        nextItem.SetFallState(true);
                        nextItem.BodyType = RigidbodyType2D.Dynamic;
                        nextItem.ColliderEnabled = true;

                        AddCreatedItem(nextItem);
                    }
                }

                _mergeCount++;
            }

            // The items stay in '_createdItems' until the command completes, so that
            // 'PoolItemAll' can still collect them when the board is prepared again.
            MergingCommand command = new MergingCommand(item01, item02, OnStart, OnComplete);
            _commands.Add(command);

            return true;
        }

        protected void ClearAllCommands()
        {
            foreach (var command in _commands)
            {
                if (command == null)
                {
                    continue;
                }

                command.Dispose();
            }

            _commands.Clear();
        }

        protected void SetHoldingItemPosition(Vector2 screenPos)
        {
            Vector2 normalizedBoardLocalPos = _boardArea.ScreenToBoardLocalNormalizedPosition(screenPos);
            Vector2 holderLocalPos = _boardArea.BoardLocalNormalizedPositionToLocal(new Vector2(normalizedBoardLocalPos.x, 1.0f));
            Rect itemAreaLocalRect = _boardArea.ItemAreaLocalRect;

            Sprite sprite = _holdingItem.Info != null ? _holdingItem.Info.Sprite : null;
            float itemScale = _holdingItem.Info != null
                ? GetItemScale(_holdingItem.Index, _holdingItem.Info)
                : 0.0f;
            float itemMinX = sprite != null ? sprite.bounds.min.x * itemScale : 0.0f;
            float itemMaxX = sprite != null ? sprite.bounds.max.x * itemScale : 0.0f;

            float minHolderX = itemAreaLocalRect.xMin - itemMinX;
            float maxHolderX = itemAreaLocalRect.xMax - itemMaxX;
            holderLocalPos.x = minHolderX <= maxHolderX
                ? Mathf.Clamp(holderLocalPos.x, minHolderX, maxHolderX)
                : (minHolderX + maxHolderX) * 0.5f;

            _holdingItem.transform.localPosition = holderLocalPos;
        }

        protected virtual float GetItemScale(int index, BoardItemInfo info)
        {
            if (info == null || _boardArea == null)
            {
                return 0.0f;
            }

            return _scaleRule.GetBoardScale(
                index,
                info.Sprite,
                _boardArea.LocalSize,
                _smallestItemBoardWidthRatio);
        }

        /// <summary>
        /// Adds an item to the board. The same instance is never registered twice,
        /// otherwise it could be pooled while another entry still keeps it alive.
        /// </summary>
        protected void AddCreatedItem(BoardItem item)
        {
            if (item == null || _createdItems.Contains(item))
            {
                return;
            }

            _createdItems.Add(item);
        }

        /// <summary>
        /// Removes an item from the board and returns it to the pool.
        /// </summary>
        protected void PoolItem(BoardItem item)
        {
            if (item == null)
            {
                return;
            }

            while (_createdItems.Remove(item))
            {
            }

            item.SetMergeState(false);
            item.gameObject.SetActive(false);

            _pool.Enqueue(item);
        }

        protected void ReleaseHoldingItem()
        {
            if (_holdingItem == null)
            {
                return;
            }

            var item = _holdingItem;
            _holdingItem = null;

            PoolItem(item);
        }

        protected void PoolItemAll()
        {
            // 'PoolItem' removes every entry of the item, so the count always decreases.
            while (_createdItems.Count > 0)
            {
                int lastIndex = _createdItems.Count - 1;
                BoardItem item = _createdItems[lastIndex];

                if (item == null)
                {
                    _createdItems.RemoveAt(lastIndex);
                    continue;
                }

                PoolItem(item);
            }
        }

        protected void InitBoardItem(BoardItem item, BoardItemInfo info)
        {
            if (item == null || info == null || _boardConfig == null)
            {
                return;
            }

            item.gameObject.SetActive(true);

            int index = _boardConfig.ItemConfig.GetIndex(info);

            item.Init(index, info);
            item.transform.localScale = Vector3.one * GetItemScale(index, info);
        }

        /// <summary>
        /// Just create board item
        /// </summary>
        protected virtual BoardItem CreateBoardItem()
        {
            if (_boardConfig == null)
            {
                Debug.LogError("BoardConfig not found.");
                return null;
            }
            if (_itemResource == null)
            {
                Debug.LogError("Resource not found.");
                return null;
            }

            BoardItem item = Instantiate(_itemResource, _boardArea.transform);

            return item;
        }
    }
}
