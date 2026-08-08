using System.Collections.Generic;
using Mu3Library.Game.WatermelonGame.Board.Config;
using Mu3Library.Game.WatermelonGame.Board.Item;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board
{
    public partial class BoardController
    {
        protected BoardItem _holdingItem;

        /// <summary>
        /// The normalized board X the held item was last placed on, the board center to begin with.
        /// </summary>
        protected float _holdingNormalizedX = 0.5f;

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
        public BoardItemInfo NextItemInfo => GetItemInfo(_nextItemIndex);

        /// <summary>
        /// The item the player is about to drop, it waits at the top of the board.
        /// </summary>
        public BoardItem HoldingItem => _holdingItem;

        /// <summary>
        /// Where the held item waits, as a fraction of the board area width.
        /// <br/> It is the place the last item was placed on while no item is held, which is where
        /// <br/> the next one appears.
        /// </summary>
        public float HoldingNormalizedX => _holdingNormalizedX;

        #region Event
        public virtual void OnDragStart(Vector2 screenPos)
        {
            if (!_isRunning || _boardArea == null)
            {
                return;
            }

            // The item normally waits at the top since the drop interval ended, it is only
            // created here when the touch came first.
            if (_holdingItem == null && !TryStandbyHoldingItem())
            {
                _boardArea.SetBoardSpawnGuideLineVisible(false);
                return;
            }

            SetHoldingItemPosition(screenPos);
            _boardArea.SetBoardSpawnGuideLineVisible(true);
        }

        public virtual void OnDragging(Vector2 screenPos)
        {
            if (_holdingItem == null || _boardArea == null)
            {
                return;
            }

            SetHoldingItemPosition(screenPos);
        }

        public virtual void OnDragEnd(Vector2 screenPos)
        {
            _boardArea?.SetBoardSpawnGuideLineVisible(false);

            if (_holdingItem == null || _boardArea == null)
            {
                return;
            }

            SetHoldingItemPosition(screenPos);

            var item = _holdingItem;
            SetHoldingItem(null);

            DropItem(item, true);

            _lastDropTime = Time.time;
            _spawnCount++;

            PlayBoardSound(BoardSoundType.ItemDrop);

            // The drop is reported once the item is falling and counted, so a listener reads the
            // board as it stands after the drop.
            OnItemDropped?.Invoke(item);
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

            // A touch may have started during the drop cooldown. The board is still dragging
            // when the item becomes available, so the guide line has to appear here as well as
            // in OnDragStart.
            if (_boardArea != null && _boardArea.IsDragging)
            {
                _boardArea.SetBoardSpawnGuideLineVisible(true);
            }
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

            BoardItemInfo info = GetItemInfo(_nextItemIndex);
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

            SetHoldingItem(item);

            // The preview always shows the item after the one now in hand.
            SetNextItemIndex(DrawNextItemIndex());

            return true;
        }

        /// <summary>
        /// Puts an item into the player's hand, or empties it with null, and reports the change
        /// <br/> through <see cref="OnHoldingItemChanged"/>.
        /// </summary>
        protected void SetHoldingItem(BoardItem item)
        {
            if (_holdingItem == item)
            {
                return;
            }

            _holdingItem = item;

            OnHoldingItemChanged?.Invoke(item);
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
                float placedNormalizedX = Mathf.Clamp01(placedNormalizedPos.x);

                // A drag reaches this every frame, and the clamp keeps the place still while the
                // finger runs past the board edge, so only a place that moved is reported.
                bool hasMoved = !Mathf.Approximately(_holdingNormalizedX, placedNormalizedX);

                _holdingNormalizedX = placedNormalizedX;

                if (hasMoved)
                {
                    OnHoldingItemMoved?.Invoke(placedNormalizedX);
                }
            }

            // The spawn marker rides on the top edge, above the middle of the held item.
            // The item transform is its pivot, so the sprite bounds give the visual center.
            _boardArea.SetSpawnLocalPositionX(holderLocalPos.x + (itemMinX + itemMaxX) * 0.5f);
        }
    }
}
