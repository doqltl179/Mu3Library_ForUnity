using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Item
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class BoardItem : MonoBehaviour
    {
        private SpriteRenderer m_renderer;
        protected SpriteRenderer _renderer => m_renderer ??= GetComponent<SpriteRenderer>();

        private CircleCollider2D m_collider;
        protected CircleCollider2D _collider => m_collider ??= GetComponent<CircleCollider2D>();
        public CircleCollider2D Collider => _collider;

        private Rigidbody2D m_rb;
        protected Rigidbody2D _rb => m_rb ??= GetComponent<Rigidbody2D>();

        public RigidbodyType2D BodyType
        {
            get => _rb.bodyType;
            set => _rb.bodyType = value;
        }

        public bool ColliderEnabled
        {
            get => _collider.enabled;
            set => _collider.enabled = value;
        }

        protected BoardItemInfo _info;
        public BoardItemInfo Info => _info;

        protected int _index = -1;
        public int Index => _index;

        protected bool _isMerging = false;
        public bool IsMerging => _isMerging;



        protected virtual void OnDisable()
        {
            _rb.bodyType = RigidbodyType2D.Kinematic;
        }

        #region Utility
        public virtual bool CanMerge(BoardItem hitItem)
        {
            if (this == null ||
                this == hitItem ||
                hitItem == null ||
                _index != hitItem.Index ||
                _info == null ||
                hitItem.Info == null ||
                !IsTouching(hitItem) ||
                _isMerging ||
                hitItem.IsMerging)
            {
                return false;
            }

            return true;
        }

        public bool IsTouching(BoardItem hitItem)
        {
            if (this == null || hitItem == null)
            {
                return false;
            }

            return _collider.IsTouching(hitItem.Collider);
        }

        public void SetMergeState(bool value) => _isMerging = value;

        public virtual void Init(int index, BoardItemInfo info)
        {
            _index = index;
            _isMerging = false;

            if (info == null)
            {
                _info = null;
                return;
            }

            _renderer.sprite = info.Sprite;

            if (info.Sprite != null)
            {
                Vector2 spriteSize = info.Sprite.bounds.size;
                _collider.radius = Mathf.Max(spriteSize.x, spriteSize.y) * 0.5f;
            }

            _info = info;
        }

        public virtual void Init(BoardItemInfo info) => Init(-1, info);
        #endregion
    }
}
