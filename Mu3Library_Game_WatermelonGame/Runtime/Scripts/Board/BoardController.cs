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

        protected BoardConfig _boardConfig;

        protected readonly List<BoardItem> _createdItems = new();
        protected BoardItem _holdingItem;

        /// <summary>
        /// The normalized board X the held item was last placed on, the board center to begin with.
        /// </summary>
        protected float _holdingNormalizedX = 0.5f;

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

        /// <summary>
        /// When the last item was released. Negative infinity means no item has been dropped yet,
        /// so the first one never has to wait.
        /// </summary>
        protected float _lastDropTime = float.NegativeInfinity;

        /// <summary>
        /// Seconds left until the next item can be spawned.
        /// </summary>
        public float DropCooldown => Mathf.Max(0.0f, _lastDropTime + _dropInterval - Time.time);

        /// <summary>
        /// False while the drop interval after the previous item has not passed yet.
        /// </summary>
        public bool CanSpawnItem => DropCooldown <= 0.0f;

        /// <summary>
        /// True while the game end check is suspended, which is exactly the drop interval
        /// after the previous item.
        /// <br/> A dropped item lands on a high stack almost immediately, so judging the board
        /// <br/> right away would end the game on a stack that is still about to slide down.
        /// </summary>
        public bool IsGameEndCheckPaused => !CanSpawnItem;

        /// <summary>
        /// How many indices were taken from the spawn rule.
        /// <br/> It runs one ahead of <see cref="SpawnCount"/> because the next item is drawn
        /// <br/> before the player gets to drop the current one.
        /// </summary>
        protected int _drawCount;

        /// <summary>
        /// The indices a restored board was saved with, the item in hand followed by the preview.
        /// <br/> They are handed out before the spawn rule is asked again, so the player keeps the
        /// <br/> items they were already promised.
        /// </summary>
        protected readonly Queue<int> _restoredItemIndices = new();

        protected int _nextItemIndex = -1;
        /// <summary>
        /// The zero-based index of the item that follows the one the player is about to drop,
        /// -1 while the board has not been prepared.
        /// </summary>
        public int NextItemIndex => _nextItemIndex;

        /// <summary>
        /// The info of the item that comes after the one the player is about to drop,
        /// meant for a preview.
        /// </summary>
        public BoardItemInfo NextItemInfo => _boardConfig != null && _nextItemIndex >= 0
            ? _boardConfig.ItemConfig.GetInfoByIndex(_nextItemIndex)
            : null;

        /// <summary>
        /// The item the player is about to drop, it waits at the top of the board.
        /// </summary>
        public BoardItem HoldingItem => _holdingItem;

        /// <summary>
        /// A misconfigured project gravity is reported once instead of on every dropped item.
        /// </summary>
        private bool _gravityWarningLogged;

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
            UpdateHoldingItem();

            // The item dropped last is still settling, so the stack is given the drop interval
            // to slide into place before it is judged.
            if (!IsGameEndCheckPaused && CheckGameEnd())
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

            Debug.Log("Game Started");

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

            Debug.Log("Game Ended");
            Debug.Log($"Score: {_score}");

            OnGameEnded?.Invoke();
        }

        public virtual void Prepare(BoardSnapshot snapshot = null)
        {
            _score = 0;
            _spawnCount = 0;
            _mergeCount = 0;
            _lastDropTime = float.NegativeInfinity;
            _holdingNormalizedX = 0.5f;

            _isPrepared = false;
            _isRunning = false;
            _isEnded = false;

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
            _boardArea.SetBoardSpawnImage(config.BoardSpawnSprite);

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

        #region Snapshot
        /// <summary>
        /// Writes the board as it stands into a snapshot, which <see cref="Prepare"/> brings back.
        /// <br/> Items that are about to merge are saved as the two items they still are, so the
        /// <br/> restored board merges them again and scores them then.
        /// </summary>
        public virtual BoardSnapshot ExportSnapshot()
        {
            BoardSnapshot snapshot = new BoardSnapshot
            {
                Score = _score,
                SpawnCount = _spawnCount,
                MergeCount = _mergeCount,

                // The held item never reached the board, it travels as an index and is put
                // back into the player's hand.
                HoldingItemIndex = _holdingItem != null ? _holdingItem.Index : -1,
                NextItemIndex = _nextItemIndex,
            };

            foreach (var item in _createdItems)
            {
                if (!TryExportItemSnapshot(item, out BoardItemSnapshot itemSnapshot))
                {
                    continue;
                }

                snapshot.BoardItemsSnapshot.Add(itemSnapshot);
            }

            return snapshot;
        }

        /// <summary>
        /// Writes the board as it stands as JSON, ready to be stored or sent.
        /// </summary>
        public string ExportSnapshotJson(bool prettyPrint = false)
            => ExportSnapshot().ToJson(prettyPrint);

        /// <summary>
        /// Prepares the board on the snapshot written by <see cref="ExportSnapshotJson"/>.
        /// <br/> An unreadable text prepares an empty board.
        /// </summary>
        public void ImportSnapshotJson(string json)
            => Prepare(BoardSnapshot.FromJson(json));

        /// <summary>
        /// Puts a saved board back on the prepared board. Called by <see cref="Prepare"/> once the
        /// <br/> board has been cleared, so it only fills in what the snapshot holds.
        /// </summary>
        protected virtual void RestoreSnapshot(BoardSnapshot snapshot)
        {
            _score = snapshot.Score;
            _spawnCount = snapshot.SpawnCount;
            _mergeCount = snapshot.MergeCount;

            // The saved items are drawn again before the spawn rule takes over.
            if (snapshot.HoldingItemIndex >= 0)
            {
                _restoredItemIndices.Enqueue(snapshot.HoldingItemIndex);
            }
            if (snapshot.NextItemIndex >= 0)
            {
                _restoredItemIndices.Enqueue(snapshot.NextItemIndex);
            }

            if (snapshot.BoardItemsSnapshot == null)
            {
                return;
            }

            if (_boardConfig == null)
            {
                Debug.LogWarning("BoardConfig not found. The saved items cannot be restored.");
                return;
            }

            foreach (var itemSnapshot in snapshot.BoardItemsSnapshot)
            {
                if (itemSnapshot == null)
                {
                    continue;
                }

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

                item.transform.SetLocalPositionAndRotation(
                    _boardArea.BoardLocalNormalizedPositionToLocal(itemSnapshot.NormalizedLocalPosition),
                    Quaternion.Euler(0.0f, 0.0f, itemSnapshot.LocalRotation));

                DropItem(item);
            }
        }

        /// <summary>
        /// Writes a single board item into a snapshot, false when it does not belong on a saved board.
        /// </summary>
        protected virtual bool TryExportItemSnapshot(BoardItem item, out BoardItemSnapshot itemSnapshot)
        {
            itemSnapshot = null;

            // The held item is not on the board yet, it is saved as 'HoldingItemIndex' instead.
            if (item == null || item == _holdingItem || item.Index < 0)
            {
                return false;
            }

            // The position is saved against the board area, which follows the screen resolution,
            // so the item lands on the same spot of the board on every device.
            if (_boardArea == null ||
                !_boardArea.TryLocalToBoardLocalNormalizedPosition(item.transform.localPosition, out Vector2 normalizedPosition))
            {
                return false;
            }

            itemSnapshot = new BoardItemSnapshot
            {
                Index = item.Index,
                NormalizedLocalPosition = normalizedPosition,

                // The board is a 2D plane, so the item only ever turns around Z.
                LocalRotation = item.transform.localEulerAngles.z,
            };

            return true;
        }
        #endregion

        #region Event
        public virtual void OnDragStart(Vector2 screenPos)
        {
            if (!_isRunning)
            {
                return;
            }

            // The item normally waits at the top since the drop interval ended, it is only
            // created here when the touch came first.
            if (_holdingItem == null && !TryStandbyHoldingItem())
            {
                return;
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

            DropItem(item, true);

            _lastDropTime = Time.time;
            _spawnCount++;
        }
        #endregion

        /// <summary>
        /// Puts the next item at the top of the board as soon as the drop interval has passed,
        /// so the player always sees what they are about to drop instead of waiting for a touch.
        /// </summary>
        protected virtual void UpdateHoldingItem()
        {
            if (_holdingItem != null || !TryStandbyHoldingItem())
            {
                return;
            }

            SetHoldingItemNormalizedX(StandbyNormalizedX);
        }

        /// <summary>
        /// Creates the item the player holds, unless the drop interval is still running.
        /// </summary>
        protected bool TryStandbyHoldingItem()
            => CanSpawnItem && TrySpawnHoldingItem();

        /// <summary>
        /// Creates the item the player holds until they release it, and draws the item that
        /// follows it so <see cref="NextItemIndex"/> keeps showing one item ahead.
        /// </summary>
        protected virtual bool TrySpawnHoldingItem()
        {
            if (_boardConfig == null)
            {
                return false;
            }

            if (_nextItemIndex < 0)
            {
                SetNextItemIndex(DrawNextItemIndex());
            }

            var info = _boardConfig.ItemConfig.GetInfoByIndex(_nextItemIndex);
            if (info == null)
            {
                return false;
            }

            var item = _pool.Dequeue(info);
            if (item == null)
            {
                return false;
            }

            item.BodyType = RigidbodyType2D.Kinematic;
            item.ColliderEnabled = false;

            _holdingItem = item;

            // The preview always shows the item after the one now in hand.
            SetNextItemIndex(DrawNextItemIndex());

            return true;
        }

        /// <summary>
        /// Takes the following index from the spawn rule. Every index is drawn exactly once,
        /// so the rule keeps seeing an unbroken count no matter when an item is dropped.
        /// <br/> A restored board hands out the indices it was saved with before the rule is asked,
        /// <br/> they were drawn from it once already.
        /// </summary>
        protected int DrawNextItemIndex()
        {
            if (_restoredItemIndices.Count > 0)
            {
                return _restoredItemIndices.Dequeue();
            }

            int index = _spawnRule.GetNextIndex(_drawCount);
            _drawCount++;

            return index;
        }

        protected void SetNextItemIndex(int index)
        {
            _nextItemIndex = index;

            OnNextItemChanged?.Invoke(index);
        }

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

                        DropItem(nextItem);
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

        /// <summary>
        /// Where the item waits before the player touches the board, as a normalized board X.
        /// <br/> It is the place the previous item was dropped from, so the spawn marker stays
        /// <br/> where the player left it instead of jumping back on every item.
        /// </summary>
        protected virtual float StandbyNormalizedX => _holdingNormalizedX;

        protected void SetHoldingItemPosition(Vector2 screenPos)
        {
            Vector2 normalizedBoardLocalPos = _boardArea.ScreenToBoardLocalNormalizedPosition(screenPos);

            SetHoldingItemNormalizedX(normalizedBoardLocalPos.x);
        }

        /// <summary>
        /// Places the held item at the top of the board on the given normalized X,
        /// keeping the whole item inside the item area.
        /// </summary>
        protected void SetHoldingItemNormalizedX(float boardNormalizedX)
        {
            if (_holdingItem == null)
            {
                return;
            }

            Vector2 holderLocalPos = _boardArea.BoardLocalNormalizedPositionToLocal(new Vector2(boardNormalizedX, 1.0f));
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

            // The item was clamped into the item area, so the place it really took is remembered.
            if (_boardArea.TryLocalToBoardLocalNormalizedPosition(holderLocalPos, out Vector2 placedNormalizedPos))
            {
                _holdingNormalizedX = Mathf.Clamp01(placedNormalizedPos.x);
            }

            // The spawn marker rides on the top edge, above the middle of the held item.
            // The item transform is its pivot, so the sprite bounds give the visual center.
            _boardArea.SetSpawnLocalPositionX(holderLocalPos.x + (itemMinX + itemMaxX) * 0.5f);
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
                _smallestItemBoardWidthRatio,
                _largestItemBoardWidthRatio);
        }

        /// <summary>
        /// The downward speed an item starts its fall with, in world units per second.
        /// <br/> It is measured against the board height, which follows the screen resolution,
        /// <br/> so every device gives the item the same push relative to the board.
        /// </summary>
        protected virtual float GetDropSpeed()
        {
            if (_boardArea == null || _dropSpeedBoardHeightRatio <= 0.0f)
            {
                return 0.0f;
            }

            return _boardArea.WorldSize.y * _dropSpeedBoardHeightRatio;
        }

        /// <summary>
        /// The downward acceleration of a falling item, in world units per second squared.
        /// <br/> Like <see cref="GetDropSpeed"/> it is measured against the board height, so the item
        /// <br/> covers the same fraction of the board in the same time on every screen resolution.
        /// </summary>
        protected virtual float GetDropAcceleration()
        {
            if (_boardArea == null || _gravityBoardHeightRatio <= 0.0f)
            {
                return 0.0f;
            }

            return _boardArea.WorldSize.y * _gravityBoardHeightRatio;
        }

        /// <summary>
        /// Scales the item's own gravity to <see cref="GetDropAcceleration"/>.
        /// <br/> Only the board items are scaled, the project gravity keeps serving the rest of the scene.
        /// </summary>
        protected void ApplyItemGravity(BoardItem item)
        {
            if (item == null)
            {
                return;
            }

            float projectGravity = Mathf.Abs(Physics2D.gravity.y);
            if (projectGravity <= Mathf.Epsilon)
            {
                if (!_gravityWarningLogged)
                {
                    _gravityWarningLogged = true;
                    Debug.LogWarning("The project gravity has no downward component, so the item gravity cannot be scaled to the board area.");
                }

                return;
            }

            item.GravityScale = GetDropAcceleration() / projectGravity;
        }

        /// <summary>
        /// Drops an item onto the board at its current position.
        /// <br/> The item falls until it lands on the board floor or on another item,
        /// <br/> so it is given the floor it can land on.
        /// </summary>
        protected void DropItem(BoardItem item)
            => DropItem(item, false);

        /// <summary>
        /// Drops an item onto the board at its current position.
        /// </summary>
        /// <param name="applyDropForce">
        /// True to push the item downward as its fall starts.
        /// <br/> Only an item the player released is pushed, an item restored from a snapshot or
        /// <br/> created by a merge appears where it belongs and just falls.
        /// </param>
        protected void DropItem(BoardItem item, bool applyDropForce)
        {
            if (item == null)
            {
                return;
            }

            item.SetFallState(true, _boardArea != null ? _boardArea.BottomOutCollider : null);
            item.BodyType = RigidbodyType2D.Dynamic;
            item.ColliderEnabled = true;

            // Every item falls with the board scaled gravity, no matter who dropped it.
            ApplyItemGravity(item);

            if (applyDropForce)
            {
                item.SetDropVelocity(Vector2.down * GetDropSpeed());
            }

            AddCreatedItem(item);
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

            // A pooled item rolled while it was on the board, so it is set upright again.
            // A restored item is turned back to its saved rotation after it leaves the pool.
            item.transform.localRotation = Quaternion.identity;
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
