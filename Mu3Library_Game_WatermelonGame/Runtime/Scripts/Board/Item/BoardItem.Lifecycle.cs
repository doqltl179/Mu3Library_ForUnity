using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Item
{
    public partial class BoardItem
    {
        private bool m_defaultStateCached;
        private Sprite m_defaultSprite;
        private float m_defaultColliderRadius;
        private Vector2 m_defaultColliderOffset;
        private bool m_defaultColliderEnabled;
        private PhysicsMaterial2D m_defaultPhysicsMaterial;
        private float m_defaultGravityScale;
        private float m_defaultLinearDamping;
        private float m_defaultAngularDamping;

        protected virtual void OnDisable()
        {
            ResetForPool();
        }

        #region Utility
        /// <summary>
        /// Restores the state that must not survive a return to the item pool.
        /// </summary>
        internal void ResetForPool()
        {
            CacheDefaultState();

            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0.0f;
            _rb.gravityScale = m_defaultGravityScale;
            _rb.linearDamping = m_defaultLinearDamping;
            _rb.angularDamping = m_defaultAngularDamping;

            _renderer.sprite = m_defaultSprite;
            _collider.radius = m_defaultColliderRadius;
            _collider.offset = m_defaultColliderOffset;
            _collider.enabled = m_defaultColliderEnabled;
            _collider.sharedMaterial = m_defaultPhysicsMaterial;

            _info = null;
            _index = -1;
            _isMerging = false;
            _mergeReservationOwner = null;
            _isFalling = false;
            _floorCollider = null;
        }

        public virtual void Init(int index, BoardItemInfo info)
        {
            CacheDefaultState();

            _index = index;
            _isMerging = false;
            _mergeReservationOwner = null;
            _info = info;

            _renderer.sprite = info != null ? info.Sprite : null;
            _collider.radius = m_defaultColliderRadius;
            _collider.offset = m_defaultColliderOffset;

            if (info != null && info.Sprite != null)
            {
                // The sprite usually carries transparent padding, so the collider is shrunk
                // to the part of it that should actually touch the other items.
                Vector2 spriteSize = info.Sprite.bounds.size;
                _collider.radius = Mathf.Max(spriteSize.x, spriteSize.y) * 0.5f * info.ColliderScale;
            }
        }

        public virtual void Init(BoardItemInfo info) => Init(-1, info);
        #endregion

        private void CacheDefaultState()
        {
            if (m_defaultStateCached)
            {
                return;
            }

            m_defaultSprite = _renderer.sprite;
            m_defaultColliderRadius = _collider.radius;
            m_defaultColliderOffset = _collider.offset;
            m_defaultColliderEnabled = _collider.enabled;
            m_defaultPhysicsMaterial = _collider.sharedMaterial;
            m_defaultGravityScale = _rb.gravityScale;
            m_defaultLinearDamping = _rb.linearDamping;
            m_defaultAngularDamping = _rb.angularDamping;
            m_defaultStateCached = true;
        }
    }
}
