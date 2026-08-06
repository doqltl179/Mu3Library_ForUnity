using System;
using System.Collections.Generic;
using System.Linq;
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
        private BoardCommandHandler m_commandHandler;
        protected BoardCommandHandler _commandHandler => m_commandHandler ??= new BoardCommandHandler(this);

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

        protected int _score;
        public int Score => _score;

        protected int _spawnCount;
        public int SpawnCount => _spawnCount;

        protected int _mergeCount;
        public int MergeCount => _mergeCount;

        public event Action OnBoardPrepared;
        public event Action OnGameStarted;
        public event Action OnGamePaused;
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

        protected virtual void OnDestory()
        {
            _commandHandler.Dispose();
        }

        protected virtual void Start()
        {
            _itemResource.gameObject.SetActive(false);
        }

        protected virtual void Update()
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

            CreateMergingCommands();
        }

        #region Utility
        public virtual void Prepare(BoardSnapshot snapshot = null)
        {
            _score = 0;
            _spawnCount = 0;
            _mergeCount = 0;

            PoolItemAll();

            _boardArea.CalculateBounds();

            if (snapshot != null)
            {
                _score = snapshot.Score;

                foreach (var itemSnapshot in snapshot.BoardItemsSnapshot)
                {
                    int itemIndex = itemSnapshot.Index;
                    var info = _boardConfig.ItemConfig.GetInfoByIndex(itemIndex);
                    if (info == null)
                    {
                        continue;
                    }

                    var item = _pool.Dequeue(info);
                    _createdItems.Add(item);

                    item.BodyType = RigidbodyType2D.Dynamic;
                    item.ColliderEnabled = true;

                    item.transform.localPosition = _boardArea.BoardLocalNormalizedPositionToLocal(itemSnapshot.NormalizedLocalPosition);
                }
            }

            Debug.Log("Game Prepared");

            OnBoardPrepared?.Invoke();
        }

        public void SetBoareConfig(BoardConfig config)
        {
            if (config == null)
            {
                return;
            }

            _boardArea.SetBoardImage(config.BoardSprite);

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
                _createdItems.Add(item);

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

            item.BodyType = RigidbodyType2D.Dynamic;
            item.ColliderEnabled = true;
        }
        #endregion

        protected void CreateMergingCommands()
        {
            for (int i = 0; i < _createdItems.Count - 1; i++)
            {
                BoardItem item01 = _createdItems[i];
                for (int j = i + 1; j < _createdItems.Count; j++)
                {
                    BoardItem item02 = _createdItems[j];
                    if (CreateMergingCommand(item01, item02))
                    {
                        i--;
                        break;
                    }
                }
            }
        }

        protected bool CreateMergingCommand(BoardItem item01, BoardItem item02)
        {
            if (!item01.CanMerge(item02))
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

                _pool.Enqueue(item01);
                _pool.Enqueue(item02);

                int nextIndex = itemIndex + 1;
                BoardItemInfo nextInfo = _boardConfig.ItemConfig.GetInfoByIndex(nextIndex);

                if (nextInfo != null)
                {
                    BoardItem nextItem = _pool.Dequeue(nextInfo);
                    _createdItems.Add(nextItem);

                    nextItem.BodyType = RigidbodyType2D.Dynamic;
                    nextItem.ColliderEnabled = true;

                    nextItem.transform.localPosition = localMiddlePos;
                }
            }

            _createdItems.Remove(item01);
            _createdItems.Remove(item02);

            MergingCommand command = new MergingCommand(item01, item02, OnStart, OnComplete);
            _commands.Add(command);

            return true;
        }

        protected void SetHoldingItemPosition(Vector2 screenPos)
        {
            Vector2 normalizedBoardLocalPos = _boardArea.ScreenToBoardLocalNormalizedPosition(screenPos);
            Vector2 holderLocalPos = _boardArea.BoardLocalNormalizedPositionToLocal(new Vector2(normalizedBoardLocalPos.x, 1.0f));

            Sprite sprite = _holdingItem.Info != null ? _holdingItem.Info.Sprite : null;
            float itemScale = _holdingItem.Info != null
                ? GetItemScale(_holdingItem.Index, _holdingItem.Info)
                : 0.0f;
            float itemMinX = sprite != null ? sprite.bounds.min.x * itemScale : 0.0f;
            float itemMaxX = sprite != null ? sprite.bounds.max.x * itemScale : 0.0f;

            float boardMinX = _boardArea.LocalLB.x;
            float boardMaxX = _boardArea.LocalRT.x;
            float minHolderX = boardMinX - itemMinX;
            float maxHolderX = boardMaxX - itemMaxX;
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

        protected void PoolItemAll()
        {
            foreach (var item in _createdItems)
            {
                if (item == null)
                {
                    return;
                }

                item.gameObject.SetActive(false);
            }

            _pool.Enqueue(_createdItems);
            _createdItems.Clear();
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
