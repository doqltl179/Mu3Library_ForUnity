using System.Collections.Generic;
using Mu3Library.Game.WatermelonGame.Board.Config;
using Mu3Library.Game.WatermelonGame.Board.Item;
using Mu3Library.ObjectPool;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board
{
    public partial class BoardController
    {
        protected readonly List<BoardItem> _createdItems = new();
        /// <summary>
        /// The items that are on the board right now, the held one excluded.
        /// <br/> It is the board's own list and not a copy, so copy it before removing anything.
        /// </summary>
        public IReadOnlyList<BoardItem> Items => _createdItems;

        private GameObjectPool<BoardItem, BoardItemInfo> m_pool;
        protected GameObjectPool<BoardItem, BoardItemInfo> _pool => m_pool ??= new GameObjectPool<BoardItem, BoardItemInfo>(CreateBoardItem, InitBoardItem);

        /// <summary>
        /// A misconfigured project gravity is reported once instead of on every dropped item.
        /// </summary>
        private bool _gravityWarningLogged;

        /// <summary>
        /// The scale an item is drawn at. The diameter range belongs to <see cref="ScaleRule"/>,
        /// <br/> the board only tells it how wide the board is.
        /// </summary>
        protected virtual float GetItemScale(int index, BoardItemInfo info)
        {
            if (info == null || _boardArea == null)
            {
                return 0.0f;
            }

            return _scaleRule.GetBoardScale(index, info.Sprite, _boardArea.LocalSize);
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
        /// Hands the item the physics the board config asks for, so every item on the board
        /// <br/> slides, rolls and bounces the same way without the item prefab carrying the values.
        /// <br/> The gravity is left out, <see cref="ApplyItemGravity"/> scales that one with the board.
        /// </summary>
        protected virtual void ApplyItemPhysics(BoardItem item, BoardConfig config)
        {
            if (item == null || config == null)
            {
                return;
            }

            // The material is optional, a config without one leaves the item prefab alone.
            if (config.ItemPhysicsMaterial != null)
            {
                item.PhysicsMaterial = config.ItemPhysicsMaterial;
            }

            BoardItemRigidbodySettings rigidbodySettings = config.ItemRigidbodySettings;

            // The damping is what stops an item from rolling on until it reaches a side wall.
            item.LinearDamping = rigidbodySettings.LinearDamping;
            item.AngularDamping = rigidbodySettings.AngularDamping;
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

            item.BodyType = RigidbodyType2D.Dynamic;
            item.ColliderEnabled = true;
            item.SetFallState(true, _boardArea != null ? _boardArea.BottomOutCollider : null);

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

            item.ResetForPool();
            item.gameObject.SetActive(false);

            _pool.Enqueue(item);
        }

        protected void ReleaseHoldingItem()
        {
            _boardArea?.SetBoardSpawnGuideLineVisible(false);

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

            item.ResetForPool();
            item.gameObject.SetActive(true);

            int index = _boardConfig.ItemConfig.GetIndex(info);
            if (index < 0)
            {
                Debug.LogError("Cannot initialize a board item that does not belong to the active board configuration.", this);
                item.ResetForPool();
                item.gameObject.SetActive(false);
                _pool.Enqueue(item);
                return;
            }

            item.Init(index, info);
            item.transform.localScale = Vector3.one * GetItemScale(index, info);

            // A pooled item rolled while it was on the board, so it is set upright again.
            // A restored item is turned back to its saved rotation after it leaves the pool.
            item.transform.localRotation = Quaternion.identity;

            ApplyItemPhysics(item, _boardConfig);
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
            if (_boardArea == null)
            {
                Debug.LogError("BoardArea not found.");
                return null;
            }

            BoardItem item = Instantiate(_itemResource, _boardArea.transform);

            return item;
        }
    }
}
